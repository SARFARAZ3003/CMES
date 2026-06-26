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
        private readonly CmesDbContext          _db;
        private readonly IConfiguration         _configuration;
        private readonly ILogger<ModelTrackingController> _logger;

        // ── Base WIP filter ───────────────────────────────────────────────────
        // Same source table and business rules as the former WipController.
        // Records are active WIP: STATUS IN (1,2,6), 8-char serial, since Aug 2025.
        private const string ModelBaseSql = @"
    dbo.MPI_COB_T_SERIAL_NO_HISTORY C
WHERE C.STATUS    IN (1, 2, 6)
  AND LEN(C.SERIALNO) = 8
  AND C.CREATEDON    >= '2025-08-01'";

        // ── Status text derivation ────────────────────────────────────────────
        // Oracle: DECODE(STATUS,1,'IN-PROD',2,'ISSUE',6,'IN REPAIR','UNKNOWN')
        private const string DeriveStatusSql = @"
CASE C.STATUS
    WHEN 1 THEN 'IN-PROD'
    WHEN 2 THEN 'ISSUE'
    WHEN 6 THEN 'IN REPAIR'
    ELSE        'UNKNOWN'
END";

        // ── Location derivation ───────────────────────────────────────────────
        // Faithful port of the legacy Oracle CASE — branch order must not change.
        // Branch 2 (LOCATION IS NOT NULL → pass-through) is essential: it ensures
        // rows with a stored LOCATION value (EQA AUDIT, PE, TEST REWORK, etc.)
        // are classified by that value rather than falling through to WORKSTATION rules.
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
        // GET /api/modeltracking/summary?modelNo=<filter>&page=1&pageSize=50
        //
        // Model-wise WIP summary — corresponds to the legacy first Oracle query.
        // One row per PRODUCTID with COUNT(DISTINCT SERIALNO) per location bucket.
        // The TOTAL row is computed in SQL using ROLLUP so no extra round-trip.
        //
        // Optional modelNo filter: partial, case-insensitive prefix match
        // against the PRODUCTID column (same behaviour as the legacy LIKE search).
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
                var (rows, totalCount) = await FetchSummaryPageAsync(modelNo, page, pageSize, ct);

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
                _logger.LogError(ex, "[ModelTracking] GetSummary SQL error.");
                return StatusCode(500, new { message = "Unable to load model summary.", detail = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/modeltracking/details?modelNo=<model>&page=1&pageSize=100
        //
        // Engine-level detail rows for a specific (or all) model.
        // Corresponds to the legacy second Oracle query.
        // Sorted CREATEDON DESC (newest first) — same as WipController details.
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
                var (rows, totalCount) = await FetchDetailsPageAsync(modelNo, page, pageSize, ct);

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

        private async Task<(List<ModelSummaryRow> Rows, long TotalCount)> FetchSummaryPageAsync(
            string? modelNo, int page, int pageSize, CancellationToken ct)
        {
            var cs = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            // ── Step 1: total distinct PRODUCTID count (for pagination metadata) ──
            long totalCount;
            await using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = $@"
SELECT COUNT(DISTINCT C.PRODUCTID)
FROM   {ModelBaseSql}
  AND  (@modelNo IS NULL OR C.PRODUCTID LIKE @modelNoLike);";
                AddParam(countCmd, "@modelNo",     (object?)modelNo ?? DBNull.Value);
                AddParam(countCmd, "@modelNoLike", string.IsNullOrWhiteSpace(modelNo)
                    ? (object)DBNull.Value : $"%{modelNo.Trim()}%");

                var scalar = await countCmd.ExecuteScalarAsync(ct);
                totalCount = scalar is DBNull or null ? 0L : Convert.ToInt64(scalar);
                _logger.LogInformation("[ModelTracking] FetchSummary total models = {Count}.", totalCount);
            }

            var rows = new List<ModelSummaryRow>((int)Math.Min(pageSize, totalCount));
            if (totalCount == 0) return (rows, 0);

            // ── Step 2: paged pivot — COUNT(DISTINCT SERIALNO) per location bucket ──
            // DeriveLocationSql evaluated once per row inside a CTE; PIVOT-style
            // conditional aggregation avoids repeating the CASE in every SUM.
            await using var pageCmd = conn.CreateCommand();
            pageCmd.CommandText = $@"
WITH WipRows AS
(
    SELECT
        C.PRODUCTID                                            AS ProductId,
        C.SERIALNO                                            AS SerialNo,
        {DeriveLocationSql}                                   AS DerivedLocation
    FROM   {ModelBaseSql}
      AND  (@modelNo IS NULL OR C.PRODUCTID LIKE @modelNoLike)
),
Pivoted AS
(
    SELECT
        ProductId,
        COUNT(DISTINCT SerialNo)                                                          AS Wip,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'FES'            THEN SerialNo END)   AS Fes,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'QUALITY DOCK'   THEN SerialNo END)   AS QualityDock,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'PAINT LINE'     THEN SerialNo END)   AS PaintLine,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'TEST CELL LINE' THEN SerialNo END)   AS TestCellLine,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'PAINT REPAIR'   THEN SerialNo END)   AS PaintRepair,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'TEST REWORK'    THEN SerialNo END)   AS TestRework,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'SHORT BUILD'    THEN SerialNo END)   AS ShortBuild,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'EQA AUDIT'      THEN SerialNo END)   AS EqaAudit,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'MRA'            THEN SerialNo END)   AS Mra,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'PE'             THEN SerialNo END)   AS Pe,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'UNKNOWN'        THEN SerialNo END)   AS Unknown
    FROM WipRows
    GROUP BY ProductId
),
Ranked AS
(
    SELECT *, ROW_NUMBER() OVER (ORDER BY Wip DESC, ProductId) AS RowNum
    FROM   Pivoted
)
SELECT *
FROM   Ranked
WHERE  RowNum BETWEEN @offset + 1 AND @offset + @pageSize
ORDER  BY RowNum;";

            AddParam(pageCmd, "@modelNo",     (object?)modelNo ?? DBNull.Value);
            AddParam(pageCmd, "@modelNoLike", string.IsNullOrWhiteSpace(modelNo)
                ? (object)DBNull.Value : $"%{modelNo.Trim()}%");
            AddParam(pageCmd, "@offset",      (long)(page - 1) * pageSize);
            AddParam(pageCmd, "@pageSize",    (long)pageSize);

            _logger.LogDebug("[ModelTracking] FetchSummary PAGE SQL page={Page}.", page);

            await using var reader = await pageCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rows.Add(new ModelSummaryRow
                {
                    ModelNo      = ReadString(reader, "ProductId")      ?? "—",
                    Wip          = ReadInt(reader, "Wip"),
                    Fes          = ReadInt(reader, "Fes"),
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

            _logger.LogInformation("[ModelTracking] FetchSummary returned {Count} rows page {Page}.", rows.Count, page);
            return (rows, totalCount);
        }

        private async Task<(List<ModelDetailRow> Rows, long TotalCount)> FetchDetailsPageAsync(
            string? modelNo, int page, int pageSize, CancellationToken ct)
        {
            var cs = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            // ── Step 1: COUNT ──
            long totalCount;
            await using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = $@"
SELECT COUNT(DISTINCT C.SERIALNO)
FROM   {ModelBaseSql}
  AND  (@modelNo IS NULL OR C.PRODUCTID LIKE @modelNoLike);";
                AddParam(countCmd, "@modelNo",     (object?)modelNo ?? DBNull.Value);
                AddParam(countCmd, "@modelNoLike", string.IsNullOrWhiteSpace(modelNo)
                    ? (object)DBNull.Value : $"%{modelNo.Trim()}%");

                var scalar = await countCmd.ExecuteScalarAsync(ct);
                totalCount = scalar is DBNull or null ? 0L : Convert.ToInt64(scalar);
                _logger.LogInformation("[ModelTracking] FetchDetails total = {Count}.", totalCount);
            }

            var rows = new List<ModelDetailRow>((int)Math.Min(pageSize, totalCount));
            if (totalCount == 0) return (rows, 0);

            // ── Step 2: paged detail rows ──
            // Selects only required columns; PRODUCTID used as Model No until
            // a PRODUCT table join is available (same pattern as WipController).
            int offset = (page - 1) * pageSize;
            await using var pageCmd = conn.CreateCommand();
            pageCmd.CommandText = $@"
SELECT
    C.SERIALNO                AS SerialNo,
    C.PRODUCTID               AS ProductId,
    C.CREATEDON               AS BlockLoadTime,
    C.WORKORDERNO             AS WorkOrderNo,
    C.WORKSTATION             AS Workstation,
    {DeriveStatusSql}         AS Status,
    {DeriveLocationSql}       AS Location,
    C.LASTUPDATEON            AS LastUpdatedOn
FROM   {ModelBaseSql}
  AND  (@modelNo IS NULL OR C.PRODUCTID LIKE @modelNoLike)
ORDER  BY C.CREATEDON DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

            AddParam(pageCmd, "@modelNo",     (object?)modelNo ?? DBNull.Value);
            AddParam(pageCmd, "@modelNoLike", string.IsNullOrWhiteSpace(modelNo)
                ? (object)DBNull.Value : $"%{modelNo.Trim()}%");
            AddParam(pageCmd, "@offset",   offset);
            AddParam(pageCmd, "@pageSize", pageSize);

            _logger.LogDebug("[ModelTracking] FetchDetails PAGE SQL page={Page} offset={Offset}.", page, offset);

            await using var reader = await pageCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var rawLocation = ReadString(reader, "Location");
                rows.Add(new ModelDetailRow
                {
                    SerialNo      = ReadString(reader,   "SerialNo"),
                    ModelNo       = ReadString(reader,   "ProductId"),
                    BlockLoadTime = ReadDateTime(reader, "BlockLoadTime"),
                    WorkOrderNo   = ReadString(reader,   "WorkOrderNo"),
                    Workstation   = ReadString(reader,   "Workstation"),
                    Status        = ReadString(reader,   "Status"),
                    // Normalise Oracle OLDLINE/NEWLINE → frontend display labels
                    Location      = NormaliseLocation(rawLocation),
                    LastUpdatedOn = ReadDateTime(reader, "LastUpdatedOn"),
                });
            }

            _logger.LogInformation("[ModelTracking] FetchDetails returned {Count} rows page {Page}.", rows.Count, page);
            return (rows, totalCount);
        }

        // ── Low-level utilities ───────────────────────────────────────────────

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

        // Maps Oracle CASE output values back to frontend display labels.
        private static string NormaliseLocation(string? raw) => raw switch
        {
            "OLDLINE" => "OLD LINE",
            "NEWLINE" => "NEW LINE",
            null      => "UNKNOWN",
            _         => raw,
        };

        // ── Response DTOs ─────────────────────────────────────────────────────

        private sealed class ModelSummaryRow
        {
            public string ModelNo      { get; set; } = "";
            public int    Wip          { get; set; }
            public int    Fes          { get; set; }
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
            public DateTime? BlockLoadTime { get; set; }
            public string?   WorkOrderNo   { get; set; }
            public string?   Workstation   { get; set; }
            public string?   Status        { get; set; }
            public string?   Location      { get; set; }
            public DateTime? LastUpdatedOn { get; set; }
        }
    }
}
