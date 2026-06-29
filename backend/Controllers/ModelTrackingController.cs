using Microsoft.AspNetCore.Mvc;
using CMES.Data;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModelTrackingController : ControllerBase
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly CmesDbContext                    _db;
        private readonly IConfiguration                   _configuration;
        private readonly ILogger<ModelTrackingController> _logger;

        // ══════════════════════════════════════════════════════════════════════
        // SQL CONSTANTS
        // Every constant is defined once and reused — never duplicated.
        // ══════════════════════════════════════════════════════════════════════

        // ── Base filtered dataset ─────────────────────────────────────────────
        // Source: MPI_COB_T_SERIAL_NO_HISTORY (contains WORKSTATION, LOCATION,
        //         PRODUCTID, CREATEDON, LASTUPDATEON).
        // LEFT JOIN so records without a MPI_PRODUCT match are never dropped.
        // STATUS IN (1,2,3,6) — exactly matches the Oracle WHERE clause.
        // Serial length = 8 — same filter as Oracle LENGTH(SERIALNO) = 8.
        // Date floor: 01-AUG-2025 — same as Oracle TO_DATE('01-AUG-2025').
        // Parameters:
        //   @fromDate — earliest CREATEDON (inclusive)
        //   @modelNo  — exact PRODUCTNO match; NULL includes all models
        private const string ModelBaseSql = @"
    dbo.MPI_COB_T_SERIAL_NO_HISTORY C
    LEFT JOIN dbo.MPI_PRODUCT P ON P.ID = C.PRODUCTID
WHERE  C.STATUS    IN (1, 2, 3, 6)
  AND  LEN(C.SERIALNO) = 8
  AND  C.CREATEDON    >= @fromDate
  AND  (@modelNo IS NULL OR P.PRODUCTNO = @modelNo)";

        // ── Status text derivation ────────────────────────────────────────────
        // Exact port of the Oracle CASE:
        //   1→IN-PROD  2→ISSUE  3→FES  6→IN REPAIR  else→UNKNOWN
        private const string DeriveStatusSql = @"
CASE C.STATUS
    WHEN 1 THEN 'IN-PROD'
    WHEN 2 THEN 'ISSUE'
    WHEN 3 THEN 'FES'
    WHEN 6 THEN 'IN REPAIR'
    ELSE        'UNKNOWN'
END";

        // ── Location derivation ───────────────────────────────────────────────
        // Exact port of the Oracle CASE (summary query version).
        // Branch order matches the Oracle query EXACTLY — do not reorder:
        //   1.  STATUS=3                           → FES
        //   2.  LOCATION='BLB REPAIR'              → TEST REWORK
        //   3.  LOCATION='PART SHORTAGE'           → SHORT BUILD
        //   4.  LOCATION IS NOT NULL               → pass through
        //        (captures QUALITY DOCK, PAINT LINE, PAINT REPAIR, MRA, PE,
        //         EQA AUDIT etc. that are stored directly in the LOCATION column.
        //         ATP REPAIR is also caught here — the check in branch 5 is dead
        //         code that matches the Oracle query exactly.)
        //   5.  WORKSTATION 10000–33400 / TC1CMW101MINIE1 / ATP REPAIR → WIP
        //        (ATP REPAIR in this branch is dead code — already caught above —
        //         kept to stay faithful to the Oracle query.)
        //   6.  WORKSTATION 40000–44600            → TEST CELL LINE
        //   7.  WORKSTATION 50000–51905            → PAINT LINE
        //   8.  PAINT REPAIR / WS 54000            → PAINT REPAIR  (dead if LOCATION set)
        //   9.  QUALITY DOCK / WS 52000,55000,...  → QUALITY DOCK  (dead if LOCATION set)
        //  10.  MRA / WS 34000                     → MRA           (dead if LOCATION set)
        //  11.  ELSE                               → UNKNOWN
        private const string DeriveLocationSql = @"
CASE
    WHEN C.STATUS = 3
        THEN 'FES'
    WHEN C.LOCATION = 'BLB REPAIR'
        THEN 'TEST REWORK'
    WHEN C.LOCATION = 'PART SHORTAGE'
        THEN 'SHORT BUILD'
    WHEN C.LOCATION IS NOT NULL
        THEN C.LOCATION
    WHEN (C.WORKSTATION >= '10000' AND C.WORKSTATION <= '33400')
      OR  C.WORKSTATION = 'TC1CMW101MINIE1'
      OR  C.LOCATION    = 'ATP REPAIR'
        THEN 'WIP'
    WHEN C.WORKSTATION >= '40000' AND C.WORKSTATION <= '44600'
        THEN 'TEST CELL LINE'
    WHEN C.WORKSTATION >= '50000' AND C.WORKSTATION <= '51905'
        THEN 'PAINT LINE'
    WHEN C.LOCATION = 'PAINT REPAIR'
      OR C.WORKSTATION = '54000'
        THEN 'PAINT REPAIR'
    WHEN C.LOCATION = 'QUALITY DOCK'
      OR C.WORKSTATION IN ('52000','55000','52100','52200')
        THEN 'QUALITY DOCK'
    WHEN C.LOCATION = 'MRA'
      OR C.WORKSTATION = '34000'
        THEN 'MRA'
    ELSE 'UNKNOWN'
END";

        // ── IST timestamp offset ──────────────────────────────────────────────
        // Oracle: CREATEDON + (5/24 + 30/(24*60)) = +5h30min = +330 minutes.
        // SQL Server equivalent used in the detail SELECT.
        private const string IstCreatedon    = "DATEADD(MINUTE, 330, C.CREATEDON)";
        private const string IstLastUpdateon = "DATEADD(MINUTE, 330, C.LASTUPDATEON)";

        // ── Cutover date — no data before the production go-live ─────────────
        private static readonly DateTime FromDate = new(2025, 8, 1);

        public ModelTrackingController(
            CmesDbContext db,
            IConfiguration configuration,
            ILogger<ModelTrackingController> logger)
        {
            _db            = db;
            _configuration = configuration;
            _logger        = logger;
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/modeltracking/summary?modelNo=<exact>&page=1&pageSize=50
        //
        // Returns one row per PRODUCTNO with COUNT per location bucket.
        // Columns: ModelNo FES WIP QualityDock PaintLine TestCellLine PaintRepair
        //          TestRework ShortBuild EqaAudit Mra Pe Unknown
        // Plus a TOTAL row computed in C# — no second table scan.
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] string? modelNo  = null,
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 50,
            CancellationToken   ct       = default)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

            _logger.LogInformation(
                "[ModelTracking] GetSummary: modelNo='{Model}' page={Page} pageSize={PageSize}.",
                modelNo ?? "(all)", page, pageSize);

            try
            {
                var (rows, totalCount, total) = await FetchSummaryPageAsync(
                    modelNo, page, pageSize, ct);

                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    items      = rows,
                    total,          // TOTAL row for the UI footer
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[ModelTracking] GetSummary SQL error.");
                return StatusCode(500, new { message = "Unable to load model summary.", detail = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/modeltracking/details?modelNo=<exact>&page=1&pageSize=100
        //
        // Returns engine-level rows for a model (or all models).
        // Columns: SerialNo ModelNo WorkOrderNo Workstation Status Location
        //          BlockLoadTime LastUpdatedOn
        // Ordered by Location ASC (matching legacy Oracle output).
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("details")]
        public async Task<IActionResult> GetDetails(
            [FromQuery] string? modelNo  = null,
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 100,
            CancellationToken   ct       = default)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);

            _logger.LogInformation(
                "[ModelTracking] GetDetails: modelNo='{Model}' page={Page} pageSize={PageSize}.",
                modelNo ?? "(all)", page, pageSize);

            try
            {
                var (rows, totalCount) = await FetchDetailsPageAsync(
                    modelNo, page, pageSize, ct);

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
                _logger.LogError(ex, "[ModelTracking] GetDetails SQL error.");
                return StatusCode(500, new { message = "Unable to load model details.", detail = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Private helpers
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ONE query does everything:
        ///   - Filters the base dataset (ModelBaseSql) exactly once.
        ///   - Derives location once per row in the Derived CTE.
        ///   - Groups and pivots in the Pivoted CTE.
        ///   - Adds ROW_NUMBER + COUNT(*) OVER () in the Ranked CTE so both
        ///     the total model count AND the current page come back in a single
        ///     round-trip — no separate COUNT query needed.
        ///
        /// TOTAL row is summed from the already-fetched page rows in C#.
        /// </summary>
        private async Task<(List<ModelSummaryRow> Rows, long TotalCount, ModelSummaryRow Total)>
            FetchSummaryPageAsync(string? modelNo, int page, int pageSize, CancellationToken ct)
        {
            var cs = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            var exactModel = NullIfBlank(modelNo);
            var offset     = (long)(page - 1) * pageSize;

            await using var cmd = conn.CreateCommand();

            // Single query:
            //   Derived  — one table scan, one join, location CASE evaluated once.
            //   Pivoted  — GROUP BY + conditional COUNT, no CASE repetition.
            //   Ranked   — ROW_NUMBER for page slice + COUNT(*) OVER() for total.
            // The outer SELECT filters to just the requested page.
            cmd.CommandText = $@"
WITH Derived AS
(
    SELECT
        P.PRODUCTNO             AS ProductNo,
        {DeriveLocationSql}     AS Location
    FROM   {ModelBaseSql}
),
Pivoted AS
(
    SELECT
        ProductNo,
        COUNT(CASE WHEN Location = 'FES'            THEN 1 END) AS Fes,
        COUNT(CASE WHEN Location = 'WIP'            THEN 1 END) AS Wip,
        COUNT(CASE WHEN Location = 'QUALITY DOCK'   THEN 1 END) AS QualityDock,
        COUNT(CASE WHEN Location = 'PAINT LINE'     THEN 1 END) AS PaintLine,
        COUNT(CASE WHEN Location = 'TEST CELL LINE' THEN 1 END) AS TestCellLine,
        COUNT(CASE WHEN Location = 'PAINT REPAIR'   THEN 1 END) AS PaintRepair,
        COUNT(CASE WHEN Location = 'TEST REWORK'    THEN 1 END) AS TestRework,
        COUNT(CASE WHEN Location = 'SHORT BUILD'    THEN 1 END) AS ShortBuild,
        COUNT(CASE WHEN Location = 'EQA AUDIT'      THEN 1 END) AS EqaAudit,
        COUNT(CASE WHEN Location = 'MRA'            THEN 1 END) AS Mra,
        COUNT(CASE WHEN Location = 'PE'             THEN 1 END) AS Pe,
        COUNT(CASE WHEN Location = 'UNKNOWN'        THEN 1 END) AS Unknown
    FROM   Derived
    WHERE  Location IS NOT NULL
    GROUP  BY ProductNo
),
Ranked AS
(
    SELECT *,
           ROW_NUMBER()  OVER (ORDER BY ProductNo) AS RowNum,
           COUNT(*)      OVER ()                   AS TotalModels
    FROM   Pivoted
)
SELECT ProductNo, Fes, Wip, QualityDock, PaintLine, TestCellLine,
       PaintRepair, TestRework, ShortBuild, EqaAudit, Mra, Pe, Unknown,
       TotalModels
FROM   Ranked
WHERE  RowNum BETWEEN @offset + 1 AND @offset + @pageSize
ORDER  BY RowNum;";

            AddParam(cmd, "@fromDate", FromDate);
            AddParam(cmd, "@modelNo",  (object?)exactModel ?? DBNull.Value);
            AddParam(cmd, "@offset",   offset);
            AddParam(cmd, "@pageSize", (long)pageSize);

            _logger.LogDebug("[ModelTracking] FetchSummary SINGLE SQL page={Page} offset={Offset} modelNo={Model}.",
                page, offset, exactModel ?? "(all)");

            var rows       = new List<ModelSummaryRow>(pageSize);
            long totalCount = 0;

            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    if (rows.Count == 0)
                        totalCount = Convert.ToInt64(reader["TotalModels"]);

                    rows.Add(new ModelSummaryRow
                    {
                        ModelNo      = ReadString(reader, "ProductNo") ?? "—",
                        Fes          = ReadInt(reader, "Fes"),
                        Wip          = ReadInt(reader, "Wip"),
                        QualityDock  = ReadInt(reader, "QualityDock"),
                        PaintLine    = ReadInt(reader, "PaintLine"),
                        TestCellLine = ReadInt(reader, "TestCellLine"),
                        PaintRepair  = ReadInt(reader, "PaintRepair"),
                        TestRework   = ReadInt(reader, "TestRework"),
                        ShortBuild   = ReadInt(reader, "ShortBuild"),
                        EqaAudit     = ReadInt(reader, "EqaAudit"),
                        Mra          = ReadInt(reader, "Mra"),
                        Pe           = ReadInt(reader, "Pe"),
                        Unknown      = ReadInt(reader, "Unknown"),
                    });
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[ModelTracking] FetchSummary SQL error.");
                throw;
            }

            _logger.LogInformation(
                "[ModelTracking] FetchSummary returned {Count} rows page {Page}, total={Total}.",
                rows.Count, page, totalCount);

            // TOTAL — summed from the fetched page; no extra DB round-trip.
            var total = new ModelSummaryRow
            {
                ModelNo      = "TOTAL",
                Fes          = rows.Sum(r => r.Fes),
                Wip          = rows.Sum(r => r.Wip),
                QualityDock  = rows.Sum(r => r.QualityDock),
                PaintLine    = rows.Sum(r => r.PaintLine),
                TestCellLine = rows.Sum(r => r.TestCellLine),
                PaintRepair  = rows.Sum(r => r.PaintRepair),
                TestRework   = rows.Sum(r => r.TestRework),
                ShortBuild   = rows.Sum(r => r.ShortBuild),
                EqaAudit     = rows.Sum(r => r.EqaAudit),
                Mra          = rows.Sum(r => r.Mra),
                Pe           = rows.Sum(r => r.Pe),
                Unknown      = rows.Sum(r => r.Unknown),
            };

            return (rows, totalCount, total);
        }

        /// <summary>
        /// ONE query does everything:
        ///   - Filters the base dataset exactly once via ModelBaseSql.
        ///   - Derives status and location once per row in the Derived CTE.
        ///   - COUNT(*) OVER () gives the total count without a second scan.
        ///   - OFFSET/FETCH pages the result inside SQL — nothing filtered in C#.
        ///   - Ordered by Location ASC, SerialNo ASC — matches Oracle ORDER BY.
        /// </summary>
        private async Task<(List<ModelDetailRow> Rows, long TotalCount)> FetchDetailsPageAsync(
            string? modelNo, int page, int pageSize, CancellationToken ct)
        {
            var cs = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            var exactModel = NullIfBlank(modelNo);
            int offset     = (page - 1) * pageSize;

            await using var cmd = conn.CreateCommand();

            // Single query:
            //   Derived CTE — one scan, one join, CASE expressions evaluated once.
            //   Outer SELECT — adds COUNT(*) OVER () for total count,
            //                  then OFFSET/FETCH for the page slice.
            // No COUNT query. No second scan. Nothing loaded into memory to filter.
            cmd.CommandText = $@"
WITH Derived AS
(
    SELECT
        C.SERIALNO                          AS SerialNo,
        P.PRODUCTNO                         AS ModelNo,
        C.WORKORDERNO                       AS WorkOrderNo,
        C.WORKSTATION                       AS Workstation,
        {DeriveStatusSql}                   AS StatusText,
        {DeriveLocationSql}                 AS Location,
        {IstCreatedon}                      AS BlockLoadTime,
        {IstLastUpdateon}                   AS LastUpdatedOn
    FROM   {ModelBaseSql}
)
SELECT
    SerialNo, ModelNo, WorkOrderNo, Workstation,
    StatusText, Location, BlockLoadTime, LastUpdatedOn,
    COUNT(*) OVER () AS TotalRows
FROM   Derived
ORDER  BY Location ASC, SerialNo ASC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

            AddParam(cmd, "@fromDate", FromDate);
            AddParam(cmd, "@modelNo",  (object?)exactModel ?? DBNull.Value);
            AddParam(cmd, "@offset",   offset);
            AddParam(cmd, "@pageSize", pageSize);

            _logger.LogDebug("[ModelTracking] FetchDetails SINGLE SQL page={Page} offset={Offset} modelNo={Model}.",
                page, offset, exactModel ?? "(all)");

            var rows       = new List<ModelDetailRow>(pageSize);
            long totalCount = 0;

            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    if (rows.Count == 0)
                        totalCount = Convert.ToInt64(reader["TotalRows"]);

                    rows.Add(new ModelDetailRow
                    {
                        SerialNo      = ReadString(reader,   "SerialNo"),
                        ModelNo       = ReadString(reader,   "ModelNo"),
                        WorkOrderNo   = ReadString(reader,   "WorkOrderNo"),
                        Workstation   = ReadString(reader,   "Workstation"),
                        Status        = ReadString(reader,   "StatusText"),
                        Location      = ReadString(reader,   "Location"),
                        BlockLoadTime = ReadDateTime(reader, "BlockLoadTime"),
                        LastUpdatedOn = ReadDateTime(reader, "LastUpdatedOn"),
                    });
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[ModelTracking] FetchDetails SQL error.");
                throw;
            }

            _logger.LogInformation(
                "[ModelTracking] FetchDetails returned {Count} rows page {Page}, total={Total}.",
                rows.Count, page, totalCount);

            return (rows, totalCount);
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

        private static DateTime? ReadDateTime(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? null : r.GetDateTime(i);
        }

        /// <summary>Returns null when the input is null or whitespace.</summary>
        private static string? NullIfBlank(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // ── Response DTOs ─────────────────────────────────────────────────────

        private sealed class ModelSummaryRow
        {
            public string ModelNo      { get; set; } = "";
            public int    Fes          { get; set; }
            public int    Wip          { get; set; }
            public int    QualityDock  { get; set; }
            public int    PaintLine    { get; set; }
            public int    TestCellLine { get; set; }
            public int    PaintRepair  { get; set; }
            public int    TestRework   { get; set; }
            public int    ShortBuild   { get; set; }
            public int    EqaAudit     { get; set; }
            public int    Mra          { get; set; }
            public int    Pe           { get; set; }
            public int    Unknown      { get; set; }
        }

        private sealed class ModelDetailRow
        {
            public string?   SerialNo      { get; set; }
            public string?   ModelNo       { get; set; }
            public string?   WorkOrderNo   { get; set; }
            public string?   Workstation   { get; set; }
            public string?   Status        { get; set; }
            public string?   Location      { get; set; }
            public DateTime? BlockLoadTime { get; set; }
            public DateTime? LastUpdatedOn { get; set; }
        }
    }
}

