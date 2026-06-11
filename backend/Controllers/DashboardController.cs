using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using CMES.Data;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        // Factory: har query apna context banata hain -> parallel chal sakti hain.
        private readonly IDbContextFactory<CmesDbContext> _factory;
        private readonly IMemoryCache _cache;
        public DashboardController(IDbContextFactory<CmesDbContext> factory, IMemoryCache cache)
        {
            _factory = factory;
            _cache = cache;
        }

        // ---- Line mapping (sir ke hisaab se) ----
        private const string OLD_LINE = "23800";     // MPI_COB_T_SERIAL_NO_HISTORY, NOT EXISTS (naya engine)
        private const string NEW_LINE = "33200";     // ditto
        private const string PAINT_LINE = "52000";   // ditto par COUNT(*) (duplicates)
        private const string TEST_CELL_WS = "40200"; // COB_T_AMI_CAPTURE_LOG, COUNT(*) (duplicates)
        // FES = MPI_COB_T_TRANSACTION_OUTBOUND ⨝ MPI_COB_T_SERIAL_NO. Shipped: source nahi - 0.

        // ---- Time: DB UTC. IST = UTC + 5:30. Shift/hour IST pe. ----
        private static readonly TimeSpan IST = new(5, 30, 0);
        private static DateTime ToIst(DateTime utc) => utc.Add(IST);

        // ---- Shifts (IST): A 06:00-14:30, B 14:30-22:30, C 22:30-06:00 ----
        private static readonly TimeSpan AStart = new(6, 0, 0);
        private static readonly TimeSpan BStart = new(14, 30, 0);
        private static readonly TimeSpan CStart = new(22, 30, 0);
        private static string ShiftOf(DateTime ist)
        {
            var t = ist.TimeOfDay;
            if (t >= AStart && t < BStart) return "A";
            if (t >= BStart && t < CStart) return "B";
            return "C";
        }

        // Business day = IST 06:00->06:00. UTC se 30 min peeche karke date (= 00:30 UTC boundary).
        private static DateTime BizDay(DateTime utc) => utc.AddMinutes(-30).Date;
        private static DateTime DayStartUtc(DateTime bizDay) => bizDay.AddMinutes(30);

        // ===== Selected-din ke scan timestamps (UTC). Har method apna context (parallel-safe). =====

        private async Task<List<DateTime>> NewEngineScans(string ws, DateTime startUtc, DateTime endUtc)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var t = db.SerialNoHistory.AsNoTracking();
            var rows = await t
                .Where(h => h.Workstation == ws && h.CreatedOn >= startUtc && h.CreatedOn < endUtc && h.SerialNo != null)
                .Where(h => !t.Any(x => x.Workstation == ws && x.SerialNo == h.SerialNo && x.CreatedOn < startUtc))
                .Select(h => new { h.SerialNo, On = h.CreatedOn!.Value })
                .ToListAsync();
            return rows.GroupBy(r => r.SerialNo).Select(g => g.Min(x => x.On)).ToList();
        }

        private async Task<List<DateTime>> PaintScans(DateTime startUtc, DateTime endUtc)
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.SerialNoHistory.AsNoTracking()
                .Where(h => h.Workstation == PAINT_LINE && h.CreatedOn >= startUtc && h.CreatedOn < endUtc)
                .Select(h => h.CreatedOn!.Value).ToListAsync();
        }

        private async Task<List<DateTime>> TestCellScans(DateTime startUtc, DateTime endUtc)
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.AmiCaptureLog.AsNoTracking()
                .Where(a => a.Workstation == TEST_CELL_WS && a.CreatedOn >= startUtc && a.CreatedOn < endUtc)
                .Select(a => a.CreatedOn!.Value).ToListAsync();
        }

        private async Task<List<DateTime>> FesScans(DateTime startUtc, DateTime endUtc)
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await (from s in db.TransactionOutbound.AsNoTracking()
                          join c in db.SerialNo.AsNoTracking() on s.WipJobNo equals c.WorkOrderNo
                          where c.SerialNo != null && c.SerialNo.Length == 8
                                && (c.Status == 3 || c.Status == 4)
                                && s.OverallStatus == 3
                                && s.CreatedOn >= startUtc && s.CreatedOn < endUtc
                          select s.CreatedOn!.Value).ToListAsync();
        }

        // Calendar range (min/max business day) - cache 2 min (rarely badalta, har call pe nahi).
        private async Task<(DateTime min, DateTime max)?> GetRangeAsync()
        {
            if (_cache.TryGetValue("range", out (DateTime, DateTime) cached)) return cached;

            await using var db = await _factory.CreateDbContextAsync();
            var maxes = new List<DateTime>(); var mins = new List<DateTime>();
            foreach (var m in new[]
            {
                await db.SerialNoHistory.MaxAsync(x => x.CreatedOn),
                await db.AmiCaptureLog.MaxAsync(x => x.CreatedOn),
                await db.TransactionOutbound.MaxAsync(x => x.CreatedOn),
            }) if (m != null) maxes.Add(m.Value);
            foreach (var m in new[]
            {
                await db.SerialNoHistory.MinAsync(x => x.CreatedOn),
                await db.AmiCaptureLog.MinAsync(x => x.CreatedOn),
                await db.TransactionOutbound.MinAsync(x => x.CreatedOn),
            }) if (m != null) mins.Add(m.Value);

            if (maxes.Count == 0) return null;
            var range = (BizDay(mins.Min()), BizDay(maxes.Max()));
            _cache.Set("range", range, TimeSpan.FromMinutes(2));
            return range;
        }

        // GET /api/Dashboard/overview?date=2026-06-10  (live refresh isi ko hit karta hain)
        [HttpGet("overview")]
        public async Task<IActionResult> Overview([FromQuery] string? date = null)
        {
            var range = await GetRangeAsync();
            if (range == null) return Ok(new { productionDay = (string?)null, message = "No data" });
            var (minDay, maxDay) = range.Value;

            var selected = maxDay;
            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsed))
                selected = parsed.Date;

            var startUtc = DayStartUtc(selected);
            var endUtc = startUtc.AddDays(1);

            // 5 metric queries PARALLEL (har ek apna context) - latency = sabse dheere wali.
            var oldT = NewEngineScans(OLD_LINE, startUtc, endUtc);
            var newT = NewEngineScans(NEW_LINE, startUtc, endUtc);
            var tcT = TestCellScans(startUtc, endUtc);
            var paintT = PaintScans(startUtc, endUtc);
            var fesT = FesScans(startUtc, endUtc);
            await Task.WhenAll(oldT, newT, tcT, paintT, fesT);

            var oldIst = oldT.Result.Select(ToIst).ToList();
            var newIst = newT.Result.Select(ToIst).ToList();
            var tcIst = tcT.Result.Select(ToIst).ToList();
            var paintIst = paintT.Result.Select(ToIst).ToList();
            var fesIst = fesT.Result.Select(ToIst).ToList();

            bool hasData = oldIst.Count + newIst.Count + tcIst.Count + paintIst.Count + fesIst.Count > 0;

            object ShiftBlock(string s) => new
            {
                oldLine = oldIst.Count(x => ShiftOf(x) == s),
                newLine = newIst.Count(x => ShiftOf(x) == s),
                testCell = tcIst.Count(x => ShiftOf(x) == s),
                paintLine = paintIst.Count(x => ShiftOf(x) == s),
                fes = fesIst.Count(x => ShiftOf(x) == s),
                shipped = 0
            };
            var shifts = new { A = ShiftBlock("A"), B = ShiftBlock("B"), C = ShiftBlock("C") };

            int[] order = { 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 0, 1, 2, 3, 4, 5 };
            var hourly = order.Select(h => new
            {
                hour = $"{h:00}:00",
                oldLine = oldIst.Count(x => x.Hour == h),
                newLine = newIst.Count(x => x.Hour == h),
                testCell = tcIst.Count(x => x.Hour == h),
            }).ToList();

            return Ok(new
            {
                productionDay = selected.ToString("yyyy-MM-dd"),
                hasData,
                minDate = minDay.ToString("yyyy-MM-dd"),
                maxDate = maxDay.ToString("yyyy-MM-dd"),
                kpis = new
                {
                    oldLine = oldIst.Count,
                    newLine = newIst.Count,
                    testCell = tcIst.Count,
                    paintLine = paintIst.Count,
                    fes = fesIst.Count,
                    shipped = 0
                },
                shifts,
                hourly
            });
        }

        // ===== Trends (daily + monthly) - heavy aggregate, cache 5 min =====
        public class DayCount { public DateTime BizDay { get; set; } public int Cnt { get; set; } }

        private async Task<List<DayCount>> PerDayNewEngines(CmesDbContext db, string ws)
        {
            var sql = @"
SELECT CAST(DATEADD(MINUTE,-30, f.FirstOn) AS DATE) AS BizDay, COUNT(*) AS Cnt
FROM (
    SELECT SERIALNO, MIN(CREATEDON) AS FirstOn
    FROM MPI_COB_T_SERIAL_NO_HISTORY
    WHERE WORKSTATION = {0} AND SERIALNO IS NOT NULL
    GROUP BY SERIALNO
) f
GROUP BY CAST(DATEADD(MINUTE,-30, f.FirstOn) AS DATE)
ORDER BY BizDay";
            return await db.Database.SqlQueryRaw<DayCount>(sql, ws).ToListAsync();
        }

        private async Task<List<DayCount>> PerDayTestCell(CmesDbContext db)
        {
            var sql = @"
SELECT CAST(DATEADD(MINUTE,-30, CREATEDON) AS DATE) AS BizDay, COUNT(*) AS Cnt
FROM COB_T_AMI_CAPTURE_LOG
WHERE WORKSTATION = {0}
GROUP BY CAST(DATEADD(MINUTE,-30, CREATEDON) AS DATE)
ORDER BY BizDay";
            return await db.Database.SqlQueryRaw<DayCount>(sql, TEST_CELL_WS).ToListAsync();
        }

        // GET /api/Dashboard/trends  -> daily (last 30) + monthly. Chart series: Old/New/Test.
        [HttpGet("trends")]
        public async Task<IActionResult> Trends()
        {
            if (_cache.TryGetValue("trends", out object? cached) && cached != null)
                return Ok(cached);

            await using var db = await _factory.CreateDbContextAsync();
            var oldMap = (await PerDayNewEngines(db, OLD_LINE)).ToDictionary(x => x.BizDay, x => x.Cnt);
            var newMap = (await PerDayNewEngines(db, NEW_LINE)).ToDictionary(x => x.BizDay, x => x.Cnt);
            var tcMap = (await PerDayTestCell(db)).ToDictionary(x => x.BizDay, x => x.Cnt);

            var days = oldMap.Keys.Concat(newMap.Keys).Concat(tcMap.Keys)
                .Distinct().OrderBy(d => d).ToList();

            var daily = days.TakeLast(30).Select(d => new
            {
                date = d.ToString("dd MMM"),
                oldLine = oldMap.GetValueOrDefault(d),
                newLine = newMap.GetValueOrDefault(d),
                testCell = tcMap.GetValueOrDefault(d),
            }).ToList();

            var monthly = days.GroupBy(d => new { d.Year, d.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    oldLine = g.Sum(d => oldMap.GetValueOrDefault(d)),
                    newLine = g.Sum(d => newMap.GetValueOrDefault(d)),
                    testCell = g.Sum(d => tcMap.GetValueOrDefault(d)),
                }).ToList();

            var result = new { daily, monthly };
            _cache.Set("trends", (object)result, TimeSpan.FromMinutes(5));
            return Ok(result);
        }
    }
}
