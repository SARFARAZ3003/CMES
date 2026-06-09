using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    }
}
