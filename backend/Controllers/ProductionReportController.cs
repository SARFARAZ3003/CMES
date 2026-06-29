using Microsoft.AspNetCore.Mvc;
using CMES.Data;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/productionreport")]
    public class ProductionReportController : ControllerBase
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly CmesDbContext                       _db;
        private readonly IConfiguration                      _configuration;
        private readonly ILogger<ProductionReportController> _logger;

        // ── Base filter ───────────────────────────────────────────────────────
        // All production queries share this FROM + WHERE fragment.
        // @date must be supplied by every query that uses it.
        // Records with 8-char serials on the given date — same table as WipController.
        private const string ProdBaseSql = @"
    dbo.MPI_COB_T_SERIAL_NO_HISTORY C
WHERE LEN(C.SERIALNO) = 8
  AND CAST(C.CREATEDON AS date) = @date";

        // ── Shift derivation ──────────────────────────────────────────────────
        // Derived from CREATEDON hour.  Used wherever shift-level breakdown needed.
        //   A: 06:00–13:59  B: 14:00–21:59  C: 22:00–05:59
        private const string DeriveShiftSql = @"
CASE
    WHEN DATEPART(HOUR, C.CREATEDON) BETWEEN 6  AND 13 THEN 'A'
    WHEN DATEPART(HOUR, C.CREATEDON) BETWEEN 14 AND 21 THEN 'B'
    ELSE 'C'
END";

        // ── Status derivation ─────────────────────────────────────────────────
        // Matches the Oracle DECODE used in the legacy application.
        // TODO: confirm status codes against the production schema.
        private const string DeriveStatusSql = @"
CASE C.STATUS
    WHEN 1 THEN 'IN-PROD'
    WHEN 2 THEN 'ISSUE'
    WHEN 6 THEN 'IN REPAIR'
    ELSE        'UNKNOWN'
END";

        // ── Location derivation ───────────────────────────────────────────────
        // Faithful port of the Oracle CASE from WipController.
        // Branch 2 (LOCATION IS NOT NULL pass-through) is essential — do not reorder.
        private const string DeriveLocationSql = @"
CASE
    WHEN C.LOCATION = 'ATP REPAIR'    THEN 'NEWLINE LOOP'
    WHEN C.LOCATION = 'BLB REPAIR'    THEN 'TEST REWORK'
    WHEN C.LOCATION = 'PART SHORTAGE' THEN 'SHORT BUILD'
    WHEN C.LOCATION IS NOT NULL       THEN C.LOCATION
    WHEN C.WORKSTATION = '10008'                                   THEN 'LINESET'
    WHEN C.WORKSTATION BETWEEN '10000' AND '13000'                 THEN 'LINESET LINE'
    WHEN C.WORKSTATION BETWEEN '20000' AND '23900'                 THEN 'OLDLINE'
    WHEN C.WORKSTATION BETWEEN '30000' AND '33200'
      OR C.WORKSTATION = 'TC1CMW101MINIE1'                         THEN 'NEWLINE'
    WHEN C.WORKSTATION BETWEEN '40000' AND '44600'                 THEN 'TEST CELL LINE'
    WHEN C.WORKSTATION BETWEEN '50000' AND '51905'                 THEN 'PAINT LINE'
    WHEN C.WORKSTATION = '54000'                                   THEN 'PAINT REPAIR'
    WHEN C.WORKSTATION IN ('52000','52100','52200','55000')         THEN 'QUALITY DOCK'
    WHEN C.WORKSTATION IN ('33300','33400')                        THEN 'NEWLINE LOOP'
    WHEN C.WORKSTATION = '34000'                                   THEN 'MRA'
    ELSE 'UNKNOWN'
END";

        public ProductionReportController(
            CmesDbContext db,
            IConfiguration configuration,
            ILogger<ProductionReportController> logger)
        {
            _db            = db;
            _configuration = configuration;
            _logger        = logger;
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/productionreport/kpis?date=YYYY-MM-DD
        //
        // Shift-wise Quant + FES counts for a given date.
        // Frontend expects:
        //   { shiftA:{quant,fes}, shiftB:{quant,fes}, shiftC:{quant,fes}, total:{quant,fes} }
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("kpis")]
        public async Task<IActionResult> GetKpis(
            [FromQuery] string? date = null,
            CancellationToken   ct   = default)
        {
            var parsedDate = TryParseDate(date, out var d) ? d : DateTime.Today;
            _logger.LogInformation("[ProductionReport] GetKpis: date={Date}.", parsedDate.ToString("yyyy-MM-dd"));

            try
            {
                var result = await FetchKpisAsync(parsedDate, ct);
                return Ok(result);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[ProductionReport] GetKpis SQL error.");
                return StatusCode(500, new { message = "Unable to load KPI summary.", detail = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/productionreport/fes?date=YYYY-MM-DD&page=1&pageSize=20
        //
        // Paginated FES records for a given date.
        // Frontend expects: { page, pageSize, totalCount, totalPages, items:[...] }
        // items shape: { sno, esn, modelNo, jobOrderNo, fesDate }
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("fes")]
        public async Task<IActionResult> GetFes(
            [FromQuery] string? date     = null,
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 20,
            CancellationToken   ct       = default)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);

            var parsedDate = TryParseDate(date, out var d) ? d : DateTime.Today;
            _logger.LogInformation(
                "[ProductionReport] GetFes: date={Date} page={Page} pageSize={PageSize}.",
                parsedDate.ToString("yyyy-MM-dd"), page, pageSize);

            try
            {
                var (rows, totalCount) = await FetchFesPageAsync(parsedDate, page, pageSize, ct);
                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    items      = rows,
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[ProductionReport] GetFes SQL error.");
                return StatusCode(500, new { message = "Unable to load FES records.", detail = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/productionreport/engine-history?esn=<ESN>&page=1&pageSize=15
        //
        // Engine info + paginated transaction history + ERP subinventory.
        // Frontend expects:
        //   { engineInfo:{modelNo,jobNo,currentLocation},
        //     transactions:{page,pageSize,totalCount,totalPages,items:[...]},
        //     erpRows:[{subinventory,qty}] }
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("engine-history")]
        public async Task<IActionResult> GetEngineHistory(
            [FromQuery] string? esn      = null,
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 15,
            CancellationToken   ct       = default)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);

            if (string.IsNullOrWhiteSpace(esn))
                return BadRequest(new { message = "esn query parameter is required." });

            var trimmedEsn = esn.Trim();
            _logger.LogInformation(
                "[ProductionReport] GetEngineHistory: esn='{Esn}' page={Page} pageSize={PageSize}.",
                trimmedEsn, page, pageSize);

            try
            {
                var result = await FetchEngineHistoryAsync(trimmedEsn, page, pageSize, ct);
                return Ok(result);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[ProductionReport] GetEngineHistory SQL error for esn='{Esn}'.", trimmedEsn);
                return StatusCode(500, new { message = "Unable to load engine history.", detail = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Private fetch helpers — one per endpoint, matching WipController style
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Single query: derive shift with DeriveShiftSql, filter with ProdBaseSql,
        /// GROUP BY shift, COUNT(DISTINCT SERIALNO) for Quant and FES.
        /// No in-memory aggregation. ProdBaseSql and DeriveShiftSql used once each.
        /// </summary>
        private async Task<object> FetchKpisAsync(DateTime date, CancellationToken ct)
        {
            var cs = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
WITH ShiftRows AS
(
    SELECT
        {DeriveShiftSql}                                          AS Shift,
        C.SERIALNO,
        C.APPLICATION
    FROM   {ProdBaseSql}
)
SELECT
    Shift,
    COUNT(DISTINCT SERIALNO)                                          AS Quant,
    COUNT(DISTINCT CASE WHEN APPLICATION = 'FES' THEN SERIALNO END)  AS Fes
FROM   ShiftRows
GROUP  BY Shift;";

            AddParam(cmd, "@date", date.Date);
            _logger.LogDebug("[ProductionReport] FetchKpis SQL:\n{Sql}", cmd.CommandText);

            int quantA = 0, fesA = 0, quantB = 0, fesB = 0, quantC = 0, fesC = 0;

            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var shift = ReadString(reader, "Shift");
                    var quant = ReadInt(reader, "Quant");
                    var fes   = ReadInt(reader, "Fes");
                    switch (shift)
                    {
                        case "A": quantA = quant; fesA = fes; break;
                        case "B": quantB = quant; fesB = fes; break;
                        case "C": quantC = quant; fesC = fes; break;
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[ProductionReport] FetchKpis SQL error.");
                throw;
            }

            _logger.LogInformation(
                "[ProductionReport] FetchKpis date={Date}: A={QA}/{FA} B={QB}/{FB} C={QC}/{FC}.",
                date.ToString("yyyy-MM-dd"), quantA, fesA, quantB, fesB, quantC, fesC);

            return new
            {
                shiftA = new { quant = quantA, fes = fesA },
                shiftB = new { quant = quantB, fes = fesB },
                shiftC = new { quant = quantC, fes = fesC },
                total  = new { quant = quantA + quantB + quantC, fes = fesA + fesB + fesC },
            };
        }

        /// <summary>
        /// Step 1: COUNT for pagination metadata.
        /// Step 2: OFFSET/FETCH page — filter first with ProdBaseSql, then select
        ///         only the columns the frontend actually needs.
        /// TODO: confirm the FES filter condition (APPLICATION = 'FES' vs STATUS code).
        /// </summary>
        private async Task<(List<FesRow> Rows, long TotalCount)> FetchFesPageAsync(
            DateTime date, int page, int pageSize, CancellationToken ct)
        {
            var cs = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            // ── Step 1: total count ───────────────────────────────────────────
            long totalCount;
            await using (var countCmd = conn.CreateCommand())
            {
                // TODO: replace APPLICATION = 'FES' with the correct FES predicate
                //       once confirmed against the production schema.
                countCmd.CommandText = $@"
SELECT COUNT(DISTINCT C.SERIALNO)
FROM   {ProdBaseSql}
  AND  C.APPLICATION = 'FES';";

                AddParam(countCmd, "@date", date.Date);
                _logger.LogDebug("[ProductionReport] FetchFes COUNT SQL (date={Date}):\n{Sql}",
                    date.ToString("yyyy-MM-dd"), countCmd.CommandText);

                try
                {
                    var scalar = await countCmd.ExecuteScalarAsync(ct);
                    totalCount = scalar is DBNull or null ? 0L : Convert.ToInt64(scalar);
                    _logger.LogInformation("[ProductionReport] FetchFes total={Count} date={Date}.",
                        totalCount, date.ToString("yyyy-MM-dd"));
                }
                catch (SqlException ex)
                {
                    _logger.LogError(ex, "[ProductionReport] FetchFes COUNT SQL error.");
                    throw;
                }
            }

            var rows = new List<FesRow>((int)Math.Min(pageSize, totalCount));
            if (totalCount == 0) return (rows, 0);

            // ── Step 2: page ──────────────────────────────────────────────────
            int offset = (page - 1) * pageSize;
            await using var pageCmd = conn.CreateCommand();

            // TODO: replace C.PRODUCTID with P.PRODUCTNO once PRODUCT table join
            //       is confirmed available in this database.
            pageCmd.CommandText = $@"
SELECT
    ROW_NUMBER() OVER (ORDER BY C.CREATEDON)  AS Sno,
    C.SERIALNO                                AS Esn,
    C.PRODUCTID                               AS ModelNo,
    C.WORKORDERNO                             AS JobOrderNo,
    C.CREATEDON                               AS FesDate
FROM   {ProdBaseSql}
  AND  C.APPLICATION = 'FES'
ORDER  BY C.CREATEDON
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

            AddParam(pageCmd, "@date",     date.Date);
            AddParam(pageCmd, "@offset",   offset);
            AddParam(pageCmd, "@pageSize", pageSize);

            _logger.LogDebug("[ProductionReport] FetchFes PAGE SQL page={Page} offset={Offset}.",
                page, offset);

            try
            {
                await using var reader = await pageCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    rows.Add(new FesRow
                    {
                        Sno        = ReadInt(reader,      "Sno"),
                        Esn        = ReadString(reader,   "Esn"),
                        ModelNo    = ReadString(reader,   "ModelNo"),
                        JobOrderNo = ReadString(reader,   "JobOrderNo"),
                        FesDate    = ReadDateTime(reader, "FesDate"),
                    });
                }
                _logger.LogInformation("[ProductionReport] FetchFes returned {Count} rows page {Page}.",
                    rows.Count, page);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[ProductionReport] FetchFes PAGE SQL error.");
                throw;
            }

            return (rows, totalCount);
        }

        /// <summary>
        /// Three focused queries, each selecting only the columns it needs:
        ///   1. Engine info   — single row, most recent record for the ESN.
        ///   2. Transactions  — paginated history rows, OFFSET/FETCH in SQL.
        ///   3. ERP rows      — small lookup, no pagination needed.
        ///
        /// TODO: replace table name placeholders and confirm column names once
        ///       the transaction and ERP tables are identified in the schema.
        /// </summary>
        private async Task<object> FetchEngineHistoryAsync(
            string esn, int page, int pageSize, CancellationToken ct)
        {
            var cs = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            // ── Query 1: engine info ──────────────────────────────────────────
            EngineInfoRow? engineInfo = null;
            await using (var infoCmd = conn.CreateCommand())
            {
                // TODO: confirm table and columns. DeriveLocationSql reused — not duplicated.
                infoCmd.CommandText = $@"
SELECT TOP 1
    C.PRODUCTID               AS ModelNo,
    C.WORKORDERNO             AS JobNo,
    {DeriveLocationSql}       AS CurrentLocation
FROM   dbo.MPI_COB_T_SERIAL_NO_HISTORY C
WHERE  C.SERIALNO = @esn
ORDER  BY C.CREATEDON DESC;";

                AddParam(infoCmd, "@esn", esn);
                _logger.LogDebug("[ProductionReport] FetchEngineHistory INFO SQL esn={Esn}.", esn);

                try
                {
                    await using var reader = await infoCmd.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        engineInfo = new EngineInfoRow
                        {
                            ModelNo         = ReadString(reader, "ModelNo"),
                            JobNo           = ReadString(reader, "JobNo"),
                            CurrentLocation = ReadString(reader, "CurrentLocation"),
                        };
                    }
                }
                catch (SqlException ex)
                {
                    _logger.LogError(ex, "[ProductionReport] FetchEngineHistory INFO SQL error.");
                    throw;
                }
            }

            _logger.LogInformation(
                "[ProductionReport] FetchEngineHistory info found={Found} esn={Esn}.",
                engineInfo is not null, esn);

            // ── Query 2: transaction history (paginated) ──────────────────────
            long txnTotal = 0;
            var txnRows = new List<TxnRow>();

            await using (var countCmd = conn.CreateCommand())
            {
                // TODO: replace dbo.MPI_COB_T_SERIAL_NO_HISTORY with the correct
                //       transaction table once identified.
                countCmd.CommandText = @"
SELECT COUNT(1)
FROM   dbo.MPI_COB_T_SERIAL_NO_HISTORY
WHERE  SERIALNO = @esn;";

                AddParam(countCmd, "@esn", esn);

                try
                {
                    var scalar = await countCmd.ExecuteScalarAsync(ct);
                    txnTotal = scalar is DBNull or null ? 0L : Convert.ToInt64(scalar);
                }
                catch (SqlException ex)
                {
                    _logger.LogError(ex, "[ProductionReport] FetchEngineHistory TXN COUNT error.");
                    throw;
                }
            }

            if (txnTotal > 0)
            {
                int offset = (page - 1) * pageSize;
                await using var txnCmd = conn.CreateCommand();

                // TODO: select the correct columns once the transaction table is confirmed.
                //       DeriveStatusSql is reused — not duplicated.
                txnCmd.CommandText = $@"
SELECT
    C.APPLICATION             AS InitCode,
    'TCL'                     AS OrgCode,
    C.WORKORDERNO             AS WipJobNo,
    C.SERIALNO                AS Esn,
    C.LOTNO                   AS ActualMsbm,
    {DeriveStatusSql}         AS Status,
    C.APPLICATION             AS OracleStatus,
    C.CREATEDON               AS ReceivedDate,
    CAST(C.ID AS bigint)      AS GroupId
FROM   dbo.MPI_COB_T_SERIAL_NO_HISTORY C
WHERE  C.SERIALNO = @esn
ORDER  BY C.CREATEDON DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

                AddParam(txnCmd, "@esn",      esn);
                AddParam(txnCmd, "@offset",   offset);
                AddParam(txnCmd, "@pageSize", pageSize);

                _logger.LogDebug("[ProductionReport] FetchEngineHistory TXN PAGE SQL page={Page}.", page);

                try
                {
                    await using var reader = await txnCmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        txnRows.Add(new TxnRow
                        {
                            InitCode     = ReadString(reader,   "InitCode"),
                            OrgCode      = ReadString(reader,   "OrgCode"),
                            WipJobNo     = ReadString(reader,   "WipJobNo"),
                            Esn          = ReadString(reader,   "Esn"),
                            ActualMsbm   = ReadString(reader,   "ActualMsbm"),
                            Status       = ReadString(reader,   "Status"),
                            OracleStatus = ReadString(reader,   "OracleStatus"),
                            ReceivedDate = ReadDateTime(reader, "ReceivedDate"),
                            GroupId      = ReadLong(reader,     "GroupId"),
                        });
                    }
                    _logger.LogInformation(
                        "[ProductionReport] FetchEngineHistory returned {Count} txn rows page {Page} esn={Esn}.",
                        txnRows.Count, page, esn);
                }
                catch (SqlException ex)
                {
                    _logger.LogError(ex, "[ProductionReport] FetchEngineHistory TXN PAGE SQL error.");
                    throw;
                }
            }

            // ── Query 3: ERP subinventory ─────────────────────────────────────
            var erpRows = new List<ErpRow>();
            await using (var erpCmd = conn.CreateCommand())
            {
                // TODO: replace with the correct ERP subinventory table and columns.
                erpCmd.CommandText = @"
SELECT  C.APPLICATION   AS Subinventory,
        COUNT(1)        AS Qty
FROM    dbo.MPI_COB_T_SERIAL_NO_HISTORY C
WHERE   C.SERIALNO = @esn
  AND   C.APPLICATION IS NOT NULL
GROUP   BY C.APPLICATION
ORDER   BY C.APPLICATION;";

                AddParam(erpCmd, "@esn", esn);
                _logger.LogDebug("[ProductionReport] FetchEngineHistory ERP SQL esn={Esn}.", esn);

                try
                {
                    await using var reader = await erpCmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        erpRows.Add(new ErpRow
                        {
                            Subinventory = ReadString(reader, "Subinventory"),
                            Qty          = ReadInt(reader,    "Qty"),
                        });
                    }
                }
                catch (SqlException ex)
                {
                    _logger.LogError(ex, "[ProductionReport] FetchEngineHistory ERP SQL error.");
                    throw;
                }
            }

            return new
            {
                engineInfo = (object?)engineInfo,
                transactions = new
                {
                    page,
                    pageSize,
                    totalCount = txnTotal,
                    totalPages = (int)Math.Ceiling((double)txnTotal / pageSize),
                    items      = txnRows,
                },
                erpRows,
            };
        }

        // ── Low-level helpers — identical style to WipController ──────────────

        private static void AddParam(IDbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value         = value;
            cmd.Parameters.Add(p);
        }

        private static string? ReadString(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i));
        }

        private static int ReadInt(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
        }

        private static long ReadLong(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? 0L : Convert.ToInt64(r.GetValue(i));
        }

        private static DateTime? ReadDateTime(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? null : r.GetDateTime(i);
        }

        private static bool TryParseDate(string? input, out DateTime result) =>
            DateTime.TryParse(input, out result);

        // ── Response DTOs ─────────────────────────────────────────────────────

        private sealed class FesRow
        {
            public int       Sno        { get; set; }
            public string?   Esn        { get; set; }
            public string?   ModelNo    { get; set; }
            public string?   JobOrderNo { get; set; }
            public DateTime? FesDate    { get; set; }
        }

        private sealed class EngineInfoRow
        {
            public string? ModelNo         { get; set; }
            public string? JobNo           { get; set; }
            public string? CurrentLocation { get; set; }
        }

        private sealed class TxnRow
        {
            public string?   InitCode     { get; set; }
            public string?   OrgCode      { get; set; }
            public string?   WipJobNo     { get; set; }
            public string?   Esn          { get; set; }
            public string?   ActualMsbm   { get; set; }
            public string?   Status       { get; set; }
            public string?   OracleStatus { get; set; }
            public DateTime? ReceivedDate { get; set; }
            public long      GroupId      { get; set; }
        }

        private sealed class ErpRow
        {
            public string? Subinventory { get; set; }
            public int     Qty          { get; set; }
        }
    }
}
