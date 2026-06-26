using Microsoft.AspNetCore.Mvc;
using CMES.Data;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CMES.Controllers
{
    /// <summary>
    /// Production Report API.
    ///
    /// Three endpoints match the frontend data contracts exactly:
    ///   GET /api/productionreport/kpis?date=YYYY-MM-DD
    ///   GET /api/productionreport/fes?date=YYYY-MM-DD&amp;page=1&amp;pageSize=20
    ///   GET /api/productionreport/engine-history?esn=&lt;ESN&gt;&amp;page=1&amp;pageSize=15
    ///
    /// SQL is intentionally absent from this skeleton.
    /// Each action method contains a clearly marked TODO block showing
    /// exactly where the SQL query goes, following the ModelTrackingController pattern.
    ///
    /// Architecture (same as ModelTrackingController):
    ///   - Filter in SQL, not in C#.
    ///   - Join only required tables.
    ///   - Aggregate (COUNT, SUM, GROUP BY) inside SQL.
    ///   - OFFSET/FETCH pagination inside SQL.
    ///   - Parameterised queries — no string concatenation.
    ///   - Common SQL fragments extracted into private const strings.
    /// </summary>
    [ApiController]
    [Route("api/productionreport")]
    public class ProductionReportController : ControllerBase
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly CmesDbContext                    _db;
        private readonly IConfiguration                  _configuration;
        private readonly ILogger<ProductionReportController> _logger;

        // ── SQL fragment placeholders (fill in when implementing queries) ──────
        //
        // These will mirror the pattern used in ModelTrackingController:
        //
        //   private const string ProdBaseSql = @"
        //       dbo.<source_table> C
        //   WHERE C.STATUS IN (...)
        //     AND C.CREATEDON = @date";
        //
        //   private const string DeriveShiftSql = @"
        //   CASE
        //       WHEN DATEPART(HOUR, C.CREATEDON) BETWEEN 6  AND 13 THEN 'A'
        //       WHEN DATEPART(HOUR, C.CREATEDON) BETWEEN 14 AND 21 THEN 'B'
        //       ELSE 'C'
        //   END";
        //
        // Add more reusable fragments here as the business logic is clarified.

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
        // Returns shift-wise and total Quant + FES counts for a given date.
        //
        // Frontend expects:
        // {
        //   "shiftA": { "quant": 97,  "fes": 98  },
        //   "shiftB": { "quant": 41,  "fes": 44  },
        //   "shiftC": { "quant": 0,   "fes": 0   },
        //   "total":  { "quant": 138, "fes": 142 }
        // }
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("kpis")]
        public async Task<IActionResult> GetKpis(
            [FromQuery] string? date = null,
            CancellationToken   ct   = default)
        {
            var parsedDate = TryParseDate(date, out var d) ? d : DateTime.Today;
            _logger.LogInformation("[ProductionReport] GetKpis: date={Date}.", parsedDate.ToString("yyyy-MM-dd"));

            // TODO: Replace this placeholder with a real SQL query.
            //
            // Pattern to follow (same as ModelTrackingController.FetchSummaryPageAsync):
            //
            //   var cs = _configuration.GetConnectionString("CMES_DB")!;
            //   await using var conn = new SqlConnection(cs);
            //   await conn.OpenAsync(ct);
            //   await using var cmd = conn.CreateCommand();
            //
            //   cmd.CommandText = $@"
            //   WITH ShiftRows AS
            //   (
            //       SELECT
            //           {DeriveShiftSql}   AS Shift,
            //           C.SERIALNO,
            //           C.STATUS
            //       FROM {ProdBaseSql}
            //   )
            //   SELECT
            //       Shift,
            //       COUNT(DISTINCT CASE WHEN STATUS = 2 THEN SERIALNO END) AS Quant,
            //       COUNT(DISTINCT CASE WHEN STATUS = 6 THEN SERIALNO END) AS Fes
            //   FROM ShiftRows
            //   GROUP BY Shift;";
            //
            //   AddParam(cmd, "@date", parsedDate.Date);
            //   // Read result into ShiftA/B/C then compute Total.

            await Task.CompletedTask; // remove when real async work is added

            return Ok(new
            {
                shiftA = new { quant = 0, fes = 0 },
                shiftB = new { quant = 0, fes = 0 },
                shiftC = new { quant = 0, fes = 0 },
                total  = new { quant = 0, fes = 0 },
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/productionreport/fes?date=YYYY-MM-DD&page=1&pageSize=20
        //
        // Returns paginated FES records for a given date.
        //
        // Frontend expects:
        // {
        //   "page": 1,
        //   "pageSize": 20,
        //   "totalCount": 98,
        //   "totalPages": 5,
        //   "items": [
        //     {
        //       "sno":        1,
        //       "esn":        "G4594528",
        //       "modelNo":    "SO64815",
        //       "jobOrderNo": "3188771-18",
        //       "fesDate":    "26-05-2026 06:29:28"
        //     }
        //   ]
        // }
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

            // TODO: Replace this placeholder with real SQL.
            //
            // Pattern (same as ModelTrackingController.FetchDetailsPageAsync):
            //
            //   // Step 1 — count
            //   cmd.CommandText = $@"
            //   SELECT COUNT(DISTINCT C.SERIALNO)
            //   FROM   {ProdBaseSql}
            //     AND  C.STATUS = 6      -- FES status
            //     AND  CAST(C.CREATEDON AS date) = @date;";
            //   AddParam(cmd, "@date", parsedDate.Date);
            //   totalCount = ...;
            //
            //   // Step 2 — page
            //   cmd.CommandText = $@"
            //   SELECT
            //       ROW_NUMBER() OVER (ORDER BY C.CREATEDON)  AS Sno,
            //       C.SERIALNO                                AS Esn,
            //       C.PRODUCTID                               AS ModelNo,
            //       C.WORKORDERNO                             AS JobOrderNo,
            //       C.CREATEDON                               AS FesDate
            //   FROM   {ProdBaseSql}
            //     AND  C.STATUS = 6
            //     AND  CAST(C.CREATEDON AS date) = @date
            //   ORDER  BY C.CREATEDON
            //   OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";
            //   AddParam(cmd, "@date",     parsedDate.Date);
            //   AddParam(cmd, "@offset",   (page - 1) * pageSize);
            //   AddParam(cmd, "@pageSize", pageSize);

            await Task.CompletedTask;

            return Ok(new
            {
                page,
                pageSize,
                totalCount = 0,
                totalPages = 0,
                items      = Array.Empty<object>(),
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /api/productionreport/engine-history?esn=<ESN>&page=1&pageSize=15
        //
        // Returns engine info + paginated transaction history + ERP subinventory
        // for a given engine serial number.
        //
        // Frontend expects:
        // {
        //   "engineInfo": {
        //     "modelNo":         "SO60341",
        //     "jobNo":           "3191793-10",
        //     "currentLocation": "NEWLINE"
        //   },
        //   "transactions": {
        //     "page": 1, "pageSize": 15, "totalCount": 18, "totalPages": 2,
        //     "items": [
        //       {
        //         "initCode":    "FES",
        //         "orgCode":     "TCL",
        //         "wipJobNo":    "3187787-1",
        //         "esn":         "G4594441",
        //         "actualMsbm":  "S001000",
        //         "status":      "FES",
        //         "oracleStatus":"COMPLETE",
        //         "receivedDate":"23-05-2026 06:10:16",
        //         "groupId":     55000
        //       }
        //     ]
        //   },
        //   "erpRows": [
        //     { "subinventory": "FES", "qty": 267 }
        //   ]
        // }
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

            // TODO: Replace this placeholder with three SQL queries:
            //
            // Query 1 — engine info (single row lookup):
            //   SELECT TOP 1
            //       C.PRODUCTID   AS ModelNo,
            //       C.WORKORDERNO AS JobNo,
            //       {DeriveLocationSql} AS CurrentLocation
            //   FROM dbo.<source_table> C
            //   WHERE C.SERIALNO = @esn
            //   ORDER BY C.CREATEDON DESC;
            //   AddParam(cmd, "@esn", trimmedEsn);
            //
            // Query 2 — transaction history (paginated):
            //   SELECT ... FROM dbo.<txn_table>
            //   WHERE SERIALNO = @esn
            //   ORDER BY CREATEDON DESC
            //   OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            //
            // Query 3 — ERP subinventory (small lookup, no pagination needed):
            //   SELECT SUBINVENTORY, QTY
            //   FROM dbo.<erp_table>
            //   WHERE SERIALNO = @esn;

            await Task.CompletedTask;

            return Ok(new
            {
                engineInfo = (object?)null,          // null → frontend shows "No engine found"
                transactions = new
                {
                    page,
                    pageSize,
                    totalCount = 0,
                    totalPages = 0,
                    items      = Array.Empty<object>(),
                },
                erpRows = Array.Empty<object>(),
            });
        }

        // ── Shared utilities (same pattern as ModelTrackingController) ────────

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

        private static bool TryParseDate(string? input, out DateTime result)
        {
            return DateTime.TryParse(input, out result);
        }
    }
}
