using Microsoft.AspNetCore.Mvc;
using CMES.Data;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WipController : ControllerBase
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly CmesDbContext          _db;
        private readonly IConfiguration         _configuration;
        private readonly ILogger<WipController> _logger;

        // ── WIP base filter ───────────────────────────────────────────────────
        // Source table is MPI_COB_T_SERIAL_NO_HISTORY — the SQL Server equivalent
        // of the Oracle COB_T_SERIAL_NO table.  MPI_COB_T_SERIAL_NO only holds
        // SERIALNO / WORKORDERNO / STATUS / CREATEDON and does NOT have WORKSTATION,
        // LOCATION, PRODUCTID, or LASTUPDATEON.
        private const string WipBaseSql = @"
    dbo.MPI_COB_T_SERIAL_NO_HISTORY C
WHERE C.STATUS   IN (1, 2, 6)
  AND LEN(C.SERIALNO) = 8
  AND C.CREATEDON    >= '2025-08-01'";

        // ── Location derivation — faithful port of the Oracle CASE expression ──
        //
        // Branch order (must not be reordered):
        //   1. Specific LOCATION text overrides (ATP REPAIR, BLB REPAIR, PART SHORTAGE)
        //   2. Pass-through: any other non-null LOCATION is used as-is
        //      (covers EQA AUDIT, PE, TEST REWORK, PAINT REPAIR, QUALITY DOCK, MRA, etc.)
        //   3. WORKSTATION numeric ranges (only reached when LOCATION IS NULL)
        //   4. ELSE UNKNOWN
        //
        // Oracle returns 'OLDLINE' and 'NEWLINE' (no spaces).
        // The frontend currently displays "OLD LINE" / "NEW LINE" (with spaces).
        // A mapping layer in NormalizeOracleLocation() reconciles them so the
        // frontend labels are preserved without touching the CASE expression.
        private const string DeriveLocationSql = @"
CASE
    -- Branch 1: specific LOCATION overrides
    WHEN C.LOCATION = 'ATP REPAIR'    THEN 'NEWLINE LOOP'
    WHEN C.LOCATION = 'BLB REPAIR'    THEN 'TEST REWORK'
    WHEN C.LOCATION = 'PART SHORTAGE' THEN 'SHORT BUILD'

    -- Branch 2: pass-through for any other non-null LOCATION (Oracle: TO_CHAR(C.LOCATION))
    WHEN C.LOCATION IS NOT NULL       THEN C.LOCATION

    -- Branch 3: WORKSTATION-range rules (only reached when LOCATION IS NULL)
    WHEN C.WORKSTATION = '10008'
        THEN 'LINESET'
    WHEN C.WORKSTATION BETWEEN '10000' AND '13000'
        THEN 'LINESET LINE'
    WHEN C.WORKSTATION BETWEEN '20000' AND '23900'
        THEN 'OLDLINE'
    WHEN C.WORKSTATION BETWEEN '30000' AND '33200'
      OR C.WORKSTATION = 'TC1CMW101MINIE1'
        THEN 'NEWLINE'
    WHEN C.WORKSTATION BETWEEN '40000' AND '44600'
        THEN 'TEST CELL LINE'
    WHEN C.WORKSTATION BETWEEN '50000' AND '51905'
        THEN 'PAINT LINE'
    WHEN C.WORKSTATION = '54000'
        THEN 'PAINT REPAIR'
    WHEN C.WORKSTATION IN ('52000','52100','52200','55000')
        THEN 'QUALITY DOCK'
    WHEN C.WORKSTATION IN ('33300','33400')
        THEN 'NEWLINE LOOP'
    WHEN C.WORKSTATION = '34000'
        THEN 'MRA'

    ELSE 'UNKNOWN'
END";

        // ── Ordered category list (matches legacy display order) ──────────────
        private static readonly (int Order, string Name)[] Categories =
        [
            ( 1, "R12 LINESET"   ),
            ( 2, "LINESET"       ),
            ( 3, "LINESET LINE"  ),
            ( 4, "OLD LINE"      ),
            ( 5, "NEW LINE"      ),
            ( 6, "TEST CELL LINE"),
            ( 7, "PAINT LINE"    ),
            ( 8, "QUALITY DOCK"  ),
            ( 9, "PAINT REPAIR"  ),
            (10, "NEWLINE LOOP"  ),
            (11, "MRA"           ),
            (12, "EQA AUDIT"     ),
            (13, "TEST REWORK"   ),
            (14, "PE"            ),
            (15, "SHORT BUILD"   ),
            (16, "UNKNOWN"       ),
        ];

        // ── Route slug → canonical name (unchanged from previous version) ─────
        private static readonly IReadOnlyDictionary<string, string> LocationRouteAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["r12lineset"]      = "R12 LINESET",
                ["r12-lineset"]     = "R12 LINESET",
                ["lineset"]         = "LINESET",
                ["linesetline"]     = "LINESET LINE",
                ["lineset-line"]    = "LINESET LINE",
                ["oldline"]         = "OLD LINE",
                ["old-line"]        = "OLD LINE",
                ["newline"]         = "NEW LINE",
                ["new-line"]        = "NEW LINE",
                ["testcell"]        = "TEST CELL LINE",
                ["test-cell"]       = "TEST CELL LINE",
                ["testcellline"]    = "TEST CELL LINE",
                ["test-cell-line"]  = "TEST CELL LINE",
                ["paintline"]       = "PAINT LINE",
                ["paint-line"]      = "PAINT LINE",
                ["paintrepair"]     = "PAINT REPAIR",
                ["paint-repair"]    = "PAINT REPAIR",
                ["qualitydock"]     = "QUALITY DOCK",
                ["quality-dock"]    = "QUALITY DOCK",
                ["newlineloop"]     = "NEWLINE LOOP",
                ["newline-loop"]    = "NEWLINE LOOP",
                ["mra"]             = "MRA",
                ["eqaaudit"]        = "EQA AUDIT",
                ["eqa-audit"]       = "EQA AUDIT",
                ["testrework"]      = "TEST REWORK",
                ["test-rework"]     = "TEST REWORK",
                ["pe"]              = "PE",
                ["shortbuild"]      = "SHORT BUILD",
                ["short-build"]     = "SHORT BUILD",
                ["unknown"]         = "UNKNOWN",
            };

        public WipController(CmesDbContext db,ILogger<WipController> logger,IConfiguration configuration)
        {
            _db = db;
            _logger = logger;
            _configuration = configuration;
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/wip/test
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("test")]
        public IActionResult Test() =>
            Ok(new { message = "WIP API is working!", timestamp = DateTime.UtcNow });

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/wip/summary
        // Returns aggregate totals derived from the live /locations data.
        // Kept for backwards compatibility with the KPI cards.
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("[WipController] GetSummary: fetching category counts.");
                var counts = await FetchCategoryCountsAsync(ct);

                long totalWip        = counts.Values.Sum();
                long activeLocations = counts.Values.Count(v => v > 0);
                int  oldestHours     = await FetchOldestHoursAsync(ct);

                _logger.LogInformation("[WipController] GetSummary: totalWip={Total} activeLocations={Locs} oldestHours={Hours}.",
                    totalWip, activeLocations, oldestHours);

                return Ok(new
                {
                    totalWip,
                    locations   = activeLocations,
                    oldestHours,
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[WipController] GetSummary SQL error.");
                return StatusCode(500, new { message = "Unable to load WIP summary.", detail = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/wip/locations
        // Returns per-category counts using COUNT(DISTINCT SERIALNO).
        // Category counts are fetched in parallel (Task.WhenAll).
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations(CancellationToken ct)
        {
            try
            {
                // Probe for PRODUCT table once so we know whether to attempt joins later
                var productExists = await ProductTableExistsAsync(ct);
                _logger.LogInformation("[WipController] GetLocations: dbo.PRODUCT exists = {Exists}.", productExists);

                _logger.LogInformation("[WipController] GetLocations: fetching category counts in parallel.");
                var counts = await FetchCategoryCountsAsync(ct);

                // NormalizeOracleLocation maps Oracle values ('OLDLINE','NEWLINE') to
                // frontend labels ('OLD LINE','NEW LINE') in the category name key.
                var result = Categories
                    .Select(c => new
                    {
                        location = NormalizeOracleLocation(c.Name),
                        count    = counts.TryGetValue(c.Name, out var n) ? n : 0L,
                    })
                    .ToList();

                _logger.LogInformation("[WipController] GetLocations: returning {Count} categories.", result.Count);
                return Ok(result);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[WipController] GetLocations SQL error.");
                return StatusCode(500, new { message = "Unable to load WIP location counts.", detail = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/wip/details[/{location}]?page=1&pageSize=100
        //
        // All filtering, sorting, and pagination is done in SQL.
        // Nothing is loaded into memory before it reaches the SELECT list.
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("details")]
        [HttpGet("details/{location}")]
        public async Task<IActionResult> GetDetails(
            string?           location = null,
            [FromQuery] int   page     = 1,
            [FromQuery] int   pageSize = 100,
            CancellationToken ct       = default)
        {
            pageSize = Math.Clamp(pageSize, 1, 500);
            page     = Math.Max(page, 1);

            var canonicalLocation = NormalizeLocationRoute(location);
            _logger.LogInformation("[WipController] GetDetails: location='{Location}' canonical='{Canonical}' page={Page} pageSize={PageSize}.",
                location ?? "(all)", canonicalLocation ?? "(all)", page, pageSize);

            try
            {
                var (rows, totalCount) = await FetchDetailPageAsync(
                    canonicalLocation, page, pageSize, ct);

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
                _logger.LogError(ex,
                    "[WipController] GetDetails SQL error for location '{Location}'.", canonicalLocation ?? "ALL");
                return StatusCode(500, new { message = "Unable to load WIP details.", detail = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Private helpers
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Runs one COUNT(DISTINCT SERIALNO) query per category in parallel.
        /// Each query uses a targeted WHERE predicate so SQL Server can use
        /// the existing indexes on WORKSTATION, LOCATION, STATUS, and CREATEDON.
        /// </summary>
        private async Task<Dictionary<string, long>> FetchCategoryCountsAsync(CancellationToken ct)
        {
            // Build one task per category
            var tasks = Categories.Select(cat => CountCategoryAsync(cat.Name, ct)).ToList();
            await Task.WhenAll(tasks);

            var result = new Dictionary<string, long>(Categories.Length);
            for (var i = 0; i < Categories.Length; i++)
                result[Categories[i].Name] = tasks[i].Result;

            return result;
        }

        /// <summary>
        /// Counts DISTINCT SERIALNOs in one category using a targeted predicate
        /// instead of the derived CASE expression, so the query is index-friendly.
        /// </summary>
        private async Task<long> CountCategoryAsync(string categoryName, CancellationToken ct)
        {
            var predicate = BuildCategoryPredicate(categoryName);
            if (predicate is null)
            {
                _logger.LogDebug("[WipController] CountCategory: no predicate for '{Category}', returning 0.", categoryName);
                return 0L;
            }

            var connectionString = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
SELECT COUNT(DISTINCT C.SERIALNO)
FROM   {WipBaseSql}
  AND  ({predicate});";

            _logger.LogDebug("[WipController] CountCategory SQL for '{Category}':\n{Sql}", categoryName, cmd.CommandText);
            try
            {
                var scalar = await cmd.ExecuteScalarAsync(ct);
                var count = scalar is DBNull or null ? 0L : Convert.ToInt64(scalar);
                _logger.LogDebug("[WipController] CountCategory '{Category}' = {Count}.", categoryName, count);
                return count;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[WipController] CountCategory SQL error for '{Category}'.", categoryName);
                throw;
            }
        }
        /// <summary>
        /// Returns a SQL predicate that matches the SAME rows DeriveLocationSql would
        /// assign to this category.  Must mirror DeriveLocationSql branch-for-branch
        /// so that CountCategoryAsync and FetchDetailPageAsync are consistent.
        ///
        /// Branch 2 of DeriveLocationSql passes non-null LOCATION through as-is,
        /// so any record with LOCATION = 'TEST CELL LINE' (for example) gets that
        /// category — and the predicate here must include that LOCATION check too.
        /// </summary>
        private static string? BuildCategoryPredicate(string category) => category switch
        {
            // ── WORKSTATION-primary categories ─────────────────────────────────
            // These have no fixed LOCATION value in the legacy data.
            // Branch 2 pass-through could still assign them if LOCATION is set,
            // so include both the LOCATION = exact-name check AND the WORKSTATION range.
            "LINESET" =>
                "(C.LOCATION = 'LINESET' OR (C.LOCATION IS NULL AND C.WORKSTATION = '10008'))",

            "LINESET LINE" =>
                "(C.LOCATION = 'LINESET LINE' OR (C.LOCATION IS NULL AND C.WORKSTATION BETWEEN '10000' AND '13000' AND C.WORKSTATION <> '10008'))",

            // Oracle CASE produces 'OLDLINE' for workstation range when LOCATION IS NULL.
            // Records with LOCATION = 'OLDLINE' also classify here via branch 2.
            "OLD LINE" =>
                "(C.LOCATION = 'OLDLINE' OR (C.LOCATION IS NULL AND C.WORKSTATION BETWEEN '20000' AND '23900'))",

            // Oracle CASE produces 'NEWLINE' for workstation range when LOCATION IS NULL.
            "NEW LINE" =>
                "(C.LOCATION = 'NEWLINE' OR (C.LOCATION IS NULL AND (C.WORKSTATION BETWEEN '30000' AND '33200' OR C.WORKSTATION = 'TC1CMW101MINIE1')))",

            // TEST CELL LINE: LOCATION-stored value OR WORKSTATION range.
            "TEST CELL LINE" =>
                "(C.LOCATION = 'TEST CELL LINE' OR (C.LOCATION IS NULL AND C.WORKSTATION BETWEEN '40000' AND '44600'))",

            // PAINT LINE: LOCATION-stored value OR WORKSTATION range.
            "PAINT LINE" =>
                "(C.LOCATION = 'PAINT LINE' OR (C.LOCATION IS NULL AND C.WORKSTATION BETWEEN '50000' AND '51905'))",

            // ── LOCATION-primary categories ────────────────────────────────────
            // These are assigned via LOCATION column (branch 2 pass-through)
            // OR via specific WORKSTATION values when LOCATION IS NULL.
            "PAINT REPAIR" =>
                "(C.LOCATION = 'PAINT REPAIR' OR (C.LOCATION IS NULL AND C.WORKSTATION = '54000'))",

            "QUALITY DOCK" =>
                "(C.LOCATION = 'QUALITY DOCK' OR (C.LOCATION IS NULL AND C.WORKSTATION IN ('52000','52100','52200','55000')))",

            // NEWLINE LOOP: branch 1 override (ATP REPAIR), or branch 2 pass-through,
            // or workstation when LOCATION IS NULL.
            "NEWLINE LOOP" =>
                "(C.LOCATION = 'ATP REPAIR' OR C.LOCATION = 'NEWLINE LOOP' OR (C.LOCATION IS NULL AND C.WORKSTATION IN ('33300','33400')))",

            "MRA" =>
                "(C.LOCATION = 'MRA' OR (C.LOCATION IS NULL AND C.WORKSTATION = '34000'))",

            // ── Override-only categories (no WORKSTATION rule) ─────────────────
            "TEST REWORK" =>
                "(C.LOCATION = 'BLB REPAIR' OR C.LOCATION = 'TEST REWORK')",

            "SHORT BUILD" =>
                "(C.LOCATION = 'PART SHORTAGE' OR C.LOCATION = 'SHORT BUILD')",

            // ── LOCATION pass-through categories (EQA AUDIT, PE) ──────────────
            // These exist only via branch 2 — records where LOCATION = these values.
            "EQA AUDIT" =>
                "C.LOCATION = 'EQA AUDIT'",

            "PE" =>
                "C.LOCATION = 'PE'",

            "R12 LINESET" =>
                "(C.LOCATION = 'R12 LINESET' OR (C.LOCATION IS NULL AND C.WORKSTATION BETWEEN '90000' AND '99999'))",

            // UNKNOWN: everything that does not match any branch above.
            // Approximated as: LOCATION IS NULL AND WORKSTATION not in any known range.
            // This is deliberately loose — exact UNKNOWN count is hard without full CASE eval.
            "UNKNOWN" =>
                @"(C.LOCATION IS NULL
                AND C.WORKSTATION NOT IN ('10008','TC1CMW101MINIE1','34000','54000')
                AND C.WORKSTATION NOT BETWEEN '10000' AND '13000'
                AND C.WORKSTATION NOT BETWEEN '20000' AND '23900'
                AND C.WORKSTATION NOT BETWEEN '30000' AND '33200'
                AND C.WORKSTATION NOT BETWEEN '40000' AND '44600'
                AND C.WORKSTATION NOT BETWEEN '50000' AND '51905'
                AND C.WORKSTATION NOT IN ('52000','52100','52200','55000','33300','33400'))",

            _ => null,
        };

        /// <summary>
        /// Fetches one page of detail rows for a given canonical location.
        /// All filtering, sorting, and pagination happens in SQL (OFFSET/FETCH).
        /// </summary>
        private async Task<(List<WipDetailItem> Rows, long TotalCount)> FetchDetailPageAsync(
            string? canonicalLocation, int page, int pageSize, CancellationToken ct)
        {
            var connectionString = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);

            // The CASE expression produces Oracle-style values ('OLDLINE', 'NEWLINE').
            // Translate the frontend canonical label to the Oracle value before filtering.
            var oracleLocation = ToOracleLocation(canonicalLocation);

            long totalCount;
            await using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = $@"
SELECT COUNT(DISTINCT C.SERIALNO)
FROM   {WipBaseSql}
  AND  (@location IS NULL OR ({DeriveLocationSql}) = @location);";
                countCmd.Parameters.AddWithValue("@location",
                    (object?)oracleLocation ?? DBNull.Value);

                _logger.LogDebug("[WipController] FetchDetailPage COUNT SQL (location={Location}):\n{Sql}",
                    oracleLocation ?? "ALL", countCmd.CommandText);
                try
                {
                    var scalar = await countCmd.ExecuteScalarAsync(ct);
                    totalCount = scalar is DBNull or null ? 0L : Convert.ToInt64(scalar);
                    _logger.LogInformation("[WipController] FetchDetailPage total count = {Count} for location '{Location}'.",
                        totalCount, oracleLocation ?? "ALL");
                }
                catch (SqlException ex)
                {
                    _logger.LogError(ex, "[WipController] FetchDetailPage COUNT SQL error for location '{Location}'.", oracleLocation ?? "ALL");
                    throw;
                }
            }

            var rows = new List<WipDetailItem>((int)Math.Min(pageSize, totalCount));

            if (totalCount > 0)
            {
                int offset = (page - 1) * pageSize;

                await using var pageCmd = conn.CreateCommand();
                pageCmd.CommandText = $@"
SELECT
    C.SERIALNO                  AS SerialNo,
    C.PRODUCTID                 AS ProductId,
    C.WORKORDERNO               AS WorkOrderNo,
    C.WORKSTATION               AS Workstation,
    CASE C.STATUS
        WHEN 1 THEN 'IN-PROD'
        WHEN 2 THEN 'ISSUE'
        WHEN 6 THEN 'IN REPAIR'
        ELSE        'UNKNOWN'
    END                         AS Status,
    {DeriveLocationSql}         AS Location,
    C.CREATEDON                 AS CreatedOn,
    C.LASTUPDATEON              AS LastUpdatedOn
FROM   {WipBaseSql}
  AND  (@location IS NULL OR ({DeriveLocationSql}) = @location)
ORDER  BY C.CREATEDON DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

                pageCmd.Parameters.AddWithValue("@location",
                    (object?)oracleLocation ?? DBNull.Value);
                pageCmd.Parameters.AddWithValue("@offset",   offset);
                pageCmd.Parameters.AddWithValue("@pageSize", pageSize);

                _logger.LogDebug("[WipController] FetchDetailPage PAGE SQL page={Page} pageSize={PageSize} location={Location}.",
                    page, pageSize, oracleLocation ?? "ALL");
                try
                {
                    await using var reader = await pageCmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        // NormalizeOracleLocation maps 'OLDLINE'→'OLD LINE', 'NEWLINE'→'NEW LINE'
                        // so the frontend receives the labels it already displays.
                        rows.Add(new WipDetailItem
                        {
                            SerialNo      = ReadString(reader,   "SerialNo"),
                            ProductId     = ReadString(reader,   "ProductId"),
                            WorkOrderNo   = ReadString(reader,   "WorkOrderNo"),
                            Workstation   = ReadString(reader,   "Workstation"),
                            Status        = ReadString(reader,   "Status"),
                            Location      = NormalizeOracleLocation(ReadString(reader, "Location")),
                            CreatedOn     = ReadDateTime(reader, "CreatedOn"),
                            LastUpdatedOn = ReadDateTime(reader, "LastUpdatedOn"),
                        });
                    }
                    _logger.LogInformation("[WipController] FetchDetailPage returned {RowCount} rows for location '{Location}' page {Page}.",
                        rows.Count, oracleLocation ?? "ALL", page);
                }
                catch (SqlException ex)
                {
                    _logger.LogError(ex, "[WipController] FetchDetailPage PAGE SQL error for location '{Location}'.", oracleLocation ?? "ALL");
                    throw;
                }
            }

            return (rows, totalCount);
        }

        /// <summary>
        /// Fetches the age in hours of the oldest WIP record.
        /// </summary>
        private async Task<int> FetchOldestHoursAsync(CancellationToken ct)
        {
            var connectionString = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
SELECT COALESCE(MAX(DATEDIFF(HOUR, C.CREATEDON, GETDATE())), 0)
FROM   {WipBaseSql};";

            _logger.LogDebug("[WipController] FetchOldestHours SQL:\n{Sql}", cmd.CommandText);
            try
            {
                var scalar = await cmd.ExecuteScalarAsync(ct);
                var hours = scalar is DBNull or null ? 0 : Convert.ToInt32(scalar);
                _logger.LogDebug("[WipController] FetchOldestHours = {Hours}h.", hours);
                return hours;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[WipController] FetchOldestHours SQL error.");
                throw;
            }
        }

        // ── Low-level helpers (unchanged) ─────────────────────────────────────

        private static string? ReadString(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i));
        }

        private static DateTime? ReadDateTime(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? null : r.GetDateTime(i);
        }

        private static string? NormalizeLocationRoute(string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return null;

            var trimmed = location.Trim();
            if (LocationRouteAliases.TryGetValue(trimmed, out var alias))
                return alias;

            var compact = trimmed.Replace(" ", "", StringComparison.Ordinal);
            if (LocationRouteAliases.TryGetValue(compact, out alias))
                return alias;

            return trimmed.Replace('-', ' ').ToUpperInvariant();
        }

        /// <summary>
        /// Maps Oracle CASE output values to the frontend display labels.
        /// Oracle returns 'OLDLINE' and 'NEWLINE'; the frontend expects 'OLD LINE'
        /// and 'NEW LINE'.  All other values are returned unchanged.
        /// </summary>
        private static string NormalizeOracleLocation(string? raw)
        {
            return raw switch
            {
                "OLDLINE" => "OLD LINE",
                "NEWLINE" => "NEW LINE",
                null      => "UNKNOWN",
                _         => raw,
            };
        }

        /// <summary>
        /// Returns the Oracle location value expected by the CASE expression for
        /// a given frontend label.  Needed when filtering details by location:
        /// the frontend sends "OLD LINE", but DeriveLocationSql produces "OLDLINE".
        /// </summary>
        private static string? ToOracleLocation(string? frontendLabel)
        {
            return frontendLabel switch
            {
                "OLD LINE" => "OLDLINE",
                "NEW LINE" => "NEWLINE",
                null       => null,
                _          => frontendLabel,
            };
        }

        /// <summary>
        /// Probes whether dbo.PRODUCT exists in the current database.
        /// Called once on startup / first use before any PRODUCT JOIN is attempted.
        /// Result is cached so the probe only runs once per application lifetime.
        /// </summary>
        private bool? _productTableExists;

        private async Task<bool> ProductTableExistsAsync(CancellationToken ct)
        {
            if (_productTableExists.HasValue)
                return _productTableExists.Value;

            var cs = _configuration.GetConnectionString("CMES_DB")!;
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT COUNT(1)
FROM   INFORMATION_SCHEMA.TABLES
WHERE  TABLE_SCHEMA = 'dbo'
  AND  TABLE_NAME   = 'PRODUCT';";
            var scalar = await cmd.ExecuteScalarAsync(ct);
            _productTableExists = Convert.ToInt32(scalar) > 0;
            _logger.LogInformation("[WipController] dbo.PRODUCT table exists: {Exists}", _productTableExists);
            return _productTableExists.Value;
        }

        // ── Response DTOs ─────────────────────────────────────────────────────

        private sealed class WipDetailItem
        {
            public string?   SerialNo      { get; set; }
            public string?   ProductId     { get; set; }
            public string?   WorkOrderNo   { get; set; }
            public string?   Workstation   { get; set; }
            public string?   Status        { get; set; }
            public string?   Location      { get; set; }
            public DateTime? CreatedOn     { get; set; }
            public DateTime? LastUpdatedOn { get; set; }
        }
    }
}
