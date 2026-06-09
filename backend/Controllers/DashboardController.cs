using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CMES.Data;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly CmesDbContext _db;

        public DashboardController(CmesDbContext db)
        {
            _db = db;
        }

        // Shift A: 06-14, B: 14-22, C: baaki (raat)
        private static string ShiftOf(int hour) =>
            hour >= 6 && hour < 14 ? "A" :
            hour >= 14 && hour < 22 ? "B" : "C";

        // Sab kuch ek call mein - real assembly data (dbo.MPI_COB_T_SERIAL_NO_HISTORY)
        // GET /api/Dashboard/overview
        [HttpGet("overview")]
        public async Task<IActionResult> Overview()
        {
            var table = _db.SerialNoHistory.AsNoTracking();

            // "Today" = data ka latest din (taaki hamesha fresh dikhe)
            var latest = await table.MaxAsync(x => x.CreatedOn);
            if (latest == null)
                return Ok(new { date = (string?)null, message = "No data" });

            var day = latest.Value.Date;
            var nextDay = day.AddDays(1);

            // Latest din ke rows memory mein (~1-2k) - aggregates yahin compute
            var dayRows = await table
                .Where(x => x.CreatedOn >= day && x.CreatedOn < nextDay
                            && x.SerialNo != null)
                .Select(x => new { x.SerialNo, x.Workstation, x.Location, x.CreatedOn })
                .ToListAsync();

            // ---- KPIs ----
            var totalRecords = await table.CountAsync();
            var uniqueSerialsAll = await table.Where(x => x.SerialNo != null)
                .Select(x => x.SerialNo).Distinct().CountAsync();
            var workstations = await table.Where(x => x.Workstation != null)
                .Select(x => x.Workstation).Distinct().CountAsync();

            var enginesToday = dayRows.Select(r => r.SerialNo).Distinct().Count();
            var eventsToday = dayRows.Count;
            var inTestCell = dayRows.Where(r => r.Location == "TEST CELL LINE")
                .Select(r => r.SerialNo).Distinct().Count();

            // ---- Shift summary (latest din) ----
            object ShiftBlock(string s)
            {
                var g = dayRows.Where(r => ShiftOf(r.CreatedOn!.Value.Hour) == s).ToList();
                return new
                {
                    ws33200 = g.Where(r => r.Workstation == "33200").Select(r => r.SerialNo).Distinct().Count(),
                    ws23800 = g.Where(r => r.Workstation == "23800").Select(r => r.SerialNo).Distinct().Count(),
                    testCell = g.Where(r => r.Location == "TEST CELL LINE").Select(r => r.SerialNo).Distinct().Count(),
                    rework = g.Where(r => r.Location == "ATP REPAIR" || r.Location == "MRA" || r.Location == "PART SHORTAGE").Select(r => r.SerialNo).Distinct().Count(),
                    engines = g.Select(r => r.SerialNo).Distinct().Count(),
                    events = g.Count
                };
            }

            var shifts = new
            {
                A = ShiftBlock("A"),
                B = ShiftBlock("B"),
                C = ShiftBlock("C")
            };

            // ---- Hourly (latest din) ----
            var hourly = Enumerable.Range(0, 24).Select(h =>
            {
                var hr = dayRows.Where(r => r.CreatedOn!.Value.Hour == h).ToList();
                return new
                {
                    hour = h.ToString(),
                    ws33200 = hr.Where(r => r.Workstation == "33200").Select(r => r.SerialNo).Distinct().Count(),
                    ws23800 = hr.Where(r => r.Workstation == "23800").Select(r => r.SerialNo).Distinct().Count(),
                };
            })
            .Where(x => x.ws33200 > 0 || x.ws23800 > 0)
            .ToList();

            // ---- Daily (har din distinct engines) ----
            var pairs = await table
                .Where(x => x.CreatedOn != null && x.SerialNo != null)
                .Select(x => new { D = x.CreatedOn!.Value.Date, x.SerialNo })
                .Distinct()
                .ToListAsync();

            var daily = pairs
                .GroupBy(p => p.D)
                .Select(grp => new { date = grp.Key.ToString("dd MMM"), engines = grp.Count() })
                .OrderBy(x => x.date)
                .ToList();

            return Ok(new
            {
                date = day.ToString("yyyy-MM-dd"),
                kpis = new
                {
                    enginesToday,
                    eventsToday,
                    inTestCell,
                    uniqueSerialsAll,
                    workstations,
                    totalRecords
                },
                shifts,
                hourly,
                daily
            });
        }
    }
}
