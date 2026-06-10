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
        public DashboardController(CmesDbContext db) => _db = db;

        // ---- Line mapping (sir ke hisaab se) ----
        // Old Line = WORKSTATION 23800, New Line = WORKSTATION 33200,
        // Test Cell = LOCATION 'TEST CELL LINE'.
        // Paint Line / FES / Shipped ka data is assembly table mein NAHI hain -
        // wo abhi 0 placeholder hain (apni alag source aayegi to yahin add karein).
        private const string OLD_LINE = "23800";
        private const string NEW_LINE = "33200";
        private const string TEST_CELL = "TEST CELL LINE";

        // ---- Shifts ----
        // A: 06:00-14:30, B: 14:30-22:30, C: 22:30-06:00 (raat, midnight cross karta hain)
        private static readonly TimeSpan AStart = new(6, 0, 0);
        private static readonly TimeSpan BStart = new(14, 30, 0);
        private static readonly TimeSpan CStart = new(22, 30, 0);

        private static string ShiftOf(DateTime t)
        {
            var tod = t.TimeOfDay;
            if (tod >= AStart && tod < BStart) return "A";
            if (tod >= BStart && tod < CStart) return "B";
            return "C"; // 22:30 - 06:00
        }

        // Production day = sir ki query jaisa: 00:30 se agle din 00:30 tak.
        // 30 min peeche karke date lo (taaki 00:00-00:30 wala pichle din mein jaaye).
        private static DateTime ProdDay(DateTime t) => t.AddMinutes(-30).Date;

        // GET /api/Dashboard/overview?date=2026-06-08
        //
        // "New engine" = sir wali query ka core: ek serial us workstation pe PEHLI baar
        // kab aaya. Pehli appearance us window mein hain to count karo. Ye exactly sir ke
        // NOT EXISTS (... CREATEDON < windowStart) ke barabar hain - re-scans hat jaate hain.
        [HttpGet("overview")]
        public async Task<IActionResult> Overview([FromQuery] string? date = null)
        {
            // Table chhoti hain (~10k rows) - zaroori columns ek baar le ke memory mein compute.
            var all = await _db.SerialNoHistory.AsNoTracking()
                .Where(x => x.SerialNo != null && x.CreatedOn != null)
                .Select(x => new { x.SerialNo, x.Workstation, x.Location, On = x.CreatedOn!.Value })
                .ToListAsync();

            if (all.Count == 0)
                return Ok(new { productionDay = (string?)null, message = "No data" });

            // Har serial ki PEHLI appearance - per workstation (Old/New line).
            var wsFirst = all
                .Where(r => r.Workstation == OLD_LINE || r.Workstation == NEW_LINE)
                .GroupBy(r => new { r.Workstation, r.SerialNo })
                .Select(g => new { g.Key.Workstation, On = g.Min(x => x.On) })
                .ToList();

            // Pehli appearance - Test Cell pe.
            var tcFirst = all.Where(r => r.Location == TEST_CELL)
                .GroupBy(r => r.SerialNo).Select(g => g.Min(x => x.On)).ToList();

            // Jin production days mein koi bhi activity (rows) hain - calendar range + empty-day check.
            // 'all' se lete hain (sirf new-engine wale din nahi) taaki 0-build din bhi calendar mein
            // zeros ke saath dikhe, beech mein gap na aaye. Truly-empty din pe hi "no data".
            var availableDays = all.Select(r => ProdDay(r.On)).Distinct().OrderBy(d => d).ToHashSet();
            var minDate = availableDays.Min();
            var maxDate = availableDays.Max();   // DB ka latest din = calendar ka max

            // Selected day: agar valid date param aaya to wahi (chahe us din data ho ya na ho),
            // warna data ka latest din. hasData batata hain us din record mile ya nahi.
            DateTime selected = maxDate;
            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsed))
                selected = parsed.Date;

            bool hasData = availableDays.Contains(selected);

            var dayStart = selected.AddMinutes(30);  // 00:30 selected day (sir ka boundary)
            var dayEnd = dayStart.AddDays(1);         // 00:30 agla din
            bool InDay(DateTime t) => t >= dayStart && t < dayEnd;

            // ---- Per-shift breakdown (selected production day) ----
            object ShiftBlock(string s)
            {
                int oldLine = wsFirst.Count(r => r.Workstation == OLD_LINE && InDay(r.On) && ShiftOf(r.On) == s);
                int newLine = wsFirst.Count(r => r.Workstation == NEW_LINE && InDay(r.On) && ShiftOf(r.On) == s);
                int testCell = tcFirst.Count(t => InDay(t) && ShiftOf(t) == s);
                return new
                {
                    oldLine,
                    newLine,
                    testCell,
                    paintLine = 0, // data source pending
                    fes = 0,       // data source pending
                    shipped = 0    // data source pending
                };
            }

            var shifts = new { A = ShiftBlock("A"), B = ShiftBlock("B"), C = ShiftBlock("C") };

            // ---- KPIs (selected production day = A+B+C) ----
            int oldDay = wsFirst.Count(r => r.Workstation == OLD_LINE && InDay(r.On));
            int newDay = wsFirst.Count(r => r.Workstation == NEW_LINE && InDay(r.On));
            int tcDay = tcFirst.Count(InDay);

            // ---- Hourly new engines (selected production day: 00:30 se agle din 00:30) ----
            // Day aadhi-raat se shuru hota hain to ghante 0 -> 23 order mein.
            int[] hourOrder = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
            var hourly = hourOrder.Select(h => new
            {
                hour = $"{h:00}:00",
                oldLine = wsFirst.Count(r => r.Workstation == OLD_LINE && InDay(r.On) && r.On.Hour == h),
                newLine = wsFirst.Count(r => r.Workstation == NEW_LINE && InDay(r.On) && r.On.Hour == h),
                testCell = tcFirst.Count(t => InDay(t) && t.Hour == h),
            }).ToList();

            // ---- Daily trend (jitne production days mein data hain) ----
            var daily = availableDays.OrderBy(d => d).Select(d =>
            {
                var ds = d.AddMinutes(30); var de = ds.AddDays(1);
                return new
                {
                    date = d.ToString("dd MMM"),
                    oldLine = wsFirst.Count(r => r.Workstation == OLD_LINE && r.On >= ds && r.On < de),
                    newLine = wsFirst.Count(r => r.Workstation == NEW_LINE && r.On >= ds && r.On < de),
                    testCell = tcFirst.Count(t => t >= ds && t < de),
                };
            }).ToList();

            // ---- Monthly trend (jitne mahine ka data hain) ----
            var monthly = availableDays
                .GroupBy(d => new { d.Year, d.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g =>
                {
                    var monthDays = g.ToHashSet();
                    return new
                    {
                        month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        oldLine = wsFirst.Count(r => r.Workstation == OLD_LINE && monthDays.Contains(ProdDay(r.On))),
                        newLine = wsFirst.Count(r => r.Workstation == NEW_LINE && monthDays.Contains(ProdDay(r.On))),
                        testCell = tcFirst.Count(t => monthDays.Contains(ProdDay(t))),
                    };
                }).ToList();

            return Ok(new
            {
                productionDay = selected.ToString("yyyy-MM-dd"),
                hasData,
                minDate = minDate.ToString("yyyy-MM-dd"),
                maxDate = maxDate.ToString("yyyy-MM-dd"),
                kpis = new
                {
                    oldLine = oldDay,
                    newLine = newDay,
                    testCell = tcDay,
                    paintLine = 0, // data source pending
                    fes = 0,       // data source pending
                    shipped = 0    // data source pending
                },
                shifts,
                hourly,
                daily,
                monthly
            });
        }
    }
}
