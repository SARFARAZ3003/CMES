using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using CMES.Data;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SerialHistoryController : ControllerBase
    {
        private readonly CmesDbContext _db;

        public SerialHistoryController(CmesDbContext db)
        {
            _db = db;
        }

        // Paginated list + optional search.
        // GET /api/SerialHistory?page=1&pageSize=50&search=64595804
        [HttpGet]
        public async Task<IActionResult> Get(int page = 1, int pageSize = 50, string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 50;

            var query = _db.SerialNoHistory.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(x =>
                    (x.SerialNo != null && x.SerialNo.Contains(s)) ||
                    (x.WorkOrderNo != null && x.WorkOrderNo.Contains(s)) ||
                    (x.Workstation != null && x.Workstation.Contains(s)) ||
                    (x.Location != null && x.Location.Contains(s)));
            }

            var total = await query.CountAsync();

            var rows = await query
                .OrderByDescending(x => x.CreatedOn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                rows
            });
        }

        // Top cards ke liye summary
        // GET /api/SerialHistory/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var total = await _db.SerialNoHistory.CountAsync();
            var serials = await _db.SerialNoHistory
                .Where(x => x.SerialNo != null)
                .Select(x => x.SerialNo).Distinct().CountAsync();
            var stations = await _db.SerialNoHistory
                .Where(x => x.Workstation != null)
                .Select(x => x.Workstation).Distinct().CountAsync();

            return Ok(new
            {
                totalRecords = total,
                uniqueSerials = serials,
                workstations = stations
            });
        }

        // Model-wise WIP summary table — matches the legacy CMES page layout.
        // Groups active WIP (STATUS IN 1,2,6  CREATEDON >= 2025-08-01) by PRODUCTID,
        // then counts how many serials are in each location category.
        //
        // GET /api/SerialHistory/model-summary
        [HttpGet("model-summary")]
        public async Task<IActionResult> GetModelSummary()
        {
            var connection = _db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            using var cmd = connection.CreateCommand();

            // Uses the same LOCATION derivation as WipController so counts are consistent.
            // Groups by PRODUCTID (temporary until PRODUCT table join is added).
            cmd.CommandText = @"
WITH WipRows AS
(
    SELECT
        C.PRODUCTID     AS ProductId,
        C.SERIALNO      AS SerialNo,
        CASE
            WHEN C.LOCATION = 'ATP REPAIR'    THEN 'NEWLINE LOOP'
            WHEN C.LOCATION = 'BLB REPAIR'    THEN 'TEST REWORK'
            WHEN C.LOCATION = 'PART SHORTAGE' THEN 'SHORT BUILD'
            WHEN C.LOCATION IS NOT NULL       THEN C.LOCATION
            WHEN C.WORKSTATION = '10008'                                    THEN 'LINESET'
            WHEN C.WORKSTATION BETWEEN '10000' AND '13000'                  THEN 'LINESET LINE'
            WHEN C.WORKSTATION BETWEEN '20000' AND '23900'                  THEN 'OLDLINE'
            WHEN C.WORKSTATION BETWEEN '30000' AND '33200'
              OR C.WORKSTATION = 'TC1CMW101MINIE1'                          THEN 'NEWLINE'
            WHEN C.WORKSTATION BETWEEN '40000' AND '44600'                  THEN 'TEST CELL LINE'
            WHEN C.WORKSTATION BETWEEN '50000' AND '51905'                  THEN 'PAINT LINE'
            WHEN C.WORKSTATION = '54000'                                    THEN 'PAINT REPAIR'
            WHEN C.WORKSTATION IN ('52000','52100','52200','55000')          THEN 'QUALITY DOCK'
            WHEN C.WORKSTATION IN ('33300','33400')                         THEN 'NEWLINE LOOP'
            WHEN C.WORKSTATION = '34000'                                    THEN 'MRA'
            ELSE 'UNKNOWN'
        END AS DerivedLocation
    FROM dbo.MPI_COB_T_SERIAL_NO_HISTORY C
    WHERE C.STATUS    IN (1, 2, 6)
      AND LEN(C.SERIALNO) = 8
      AND C.CREATEDON >= '2025-08-01'
),
Pivoted AS
(
    SELECT
        ProductId,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'LINESET'        THEN SerialNo END) AS FES,
        COUNT(DISTINCT SerialNo)                                                         AS WIP,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'QUALITY DOCK'   THEN SerialNo END) AS QualityDock,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'PAINT LINE'     THEN SerialNo END) AS PaintLine,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'TEST CELL LINE' THEN SerialNo END) AS TestCellLine,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'PAINT REPAIR'   THEN SerialNo END) AS PaintRepair,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'TEST REWORK'    THEN SerialNo END) AS TestRework,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'SHORT BUILD'    THEN SerialNo END) AS ShortBuild,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'EQA AUDIT'      THEN SerialNo END) AS EqaAudit,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'MRA'            THEN SerialNo END) AS Mra,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'PE'             THEN SerialNo END) AS Pe,
        COUNT(DISTINCT CASE WHEN DerivedLocation = 'UNKNOWN'        THEN SerialNo END) AS Unknown
    FROM WipRows
    GROUP BY ProductId
)
SELECT *
FROM   Pivoted
WHERE  WIP > 0
ORDER  BY WIP DESC;";

            using var reader = await cmd.ExecuteReaderAsync();
            var rows = new List<object>();
            while (await reader.ReadAsync())
            {
                rows.Add(new
                {
                    modelNo      = reader["ProductId"]?.ToString() ?? "—",
                    fes          = Convert.ToInt32(reader["FES"]),
                    wip          = Convert.ToInt32(reader["WIP"]),
                    qualityDock  = Convert.ToInt32(reader["QualityDock"]),
                    paintLine    = Convert.ToInt32(reader["PaintLine"]),
                    testCellLine = Convert.ToInt32(reader["TestCellLine"]),
                    paintRepair  = Convert.ToInt32(reader["PaintRepair"]),
                    testRework   = Convert.ToInt32(reader["TestRework"]),
                    shortBuild   = Convert.ToInt32(reader["ShortBuild"]),
                    eqaAudit     = Convert.ToInt32(reader["EqaAudit"]),
                    mra          = Convert.ToInt32(reader["Mra"]),
                    pe           = Convert.ToInt32(reader["Pe"]),
                    unknown      = Convert.ToInt32(reader["Unknown"]),
                });
            }
            return Ok(rows);
        }
    }
}
