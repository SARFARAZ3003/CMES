using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using CMES.Data;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "CmesUser")]   // sirf active CMES user dashboard data dekh sakta hain
    public class DashboardController : ControllerBase
    {
        private readonly IDbContextFactory<CmesDbContext> _factory;
        private readonly IMemoryCache _cache;
        public DashboardController(IDbContextFactory<CmesDbContext> factory, IMemoryCache cache)
        {
            _factory = factory;
            _cache = cache;
        }

        // ---- Line mapping (sir ke hisaab se) ----
        private const string OLD_LINE = "23800";     // MPI_COB_T_SERIAL_NO_HISTORY, distinct new engine (NOT EXISTS)
        private const string NEW_LINE = "33200";     // ditto
        private const string PAINT_LINE = "52000";   // ditto, din ke andar distinct by SERIALNO
        private const string TEST_CELL_WS = "40200"; // COB_T_AMI_CAPTURE_LOG, COUNT(*) (har scan)
        // FES = MPI_COB_T_TRANSACTION_OUTBOUND ⨝ MPI_COB_T_SERIAL_NO. Shipped: source nahi - 0.

        // Business day = IST 06:00->06:00. UTC se 30 min peeche karke date (= 00:30 UTC boundary).
        private static DateTime BizDay(DateTime utc) => utc.AddMinutes(-30).Date;
        private static DateTime DayStartUtc(DateTime bizDay) => bizDay.AddMinutes(30);

        // ===== SQL-side aggregation: counting DB mein hoti hain, rows fetch NAHI hote. =====
        // IST = UTC + 330 min. Shift/hour SQL ke andar compute. Result chhota (~26 rows/metric).
        private static string IstExpr(string col) => $"DATEADD(MINUTE,330,{col})";
        private static string ShiftExpr(string col) =>
            $"CASE WHEN CAST({IstExpr(col)} AS TIME) >= '06:00:00' AND CAST({IstExpr(col)} AS TIME) < '14:30:00' THEN 'A' " +
            $"WHEN CAST({IstExpr(col)} AS TIME) >= '14:30:00' AND CAST({IstExpr(col)} AS TIME) < '22:30:00' THEN 'B' ELSE 'C' END";
        private static string HourExpr(string col) => $"DATEPART(HOUR, {IstExpr(col)})";

        // SQL aggregate result: per (shift, hour) count.
        public class ShiftHourCount { public string Shift { get; set; } = ""; public int Hour { get; set; } public int Cnt { get; set; } }

        private async Task<List<ShiftHourCount>> RunAgg(string sql, params object[] ps)
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Database.SqlQueryRaw<ShiftHourCount>(sql, ps).ToListAsync();
        }

        // Old/New line: DISTINCT naya engine (NOT EXISTS = pehli baar us line pe). SQL GROUP BY.
        private Task<List<ShiftHourCount>> NewEngineAgg(string ws, DateTime s, DateTime e)
        {
            var sql = $@"
            SELECT {ShiftExpr("t.f")} AS Shift, {HourExpr("t.f")} AS Hour, COUNT(*) AS Cnt
            FROM (
                SELECT MIN(h.CREATEDON) AS f
                FROM MPI_COB_T_SERIAL_NO_HISTORY h
                WHERE h.WORKSTATION = {{0}} AND h.CREATEDON >= {{1}} AND h.CREATEDON < {{2}} AND h.SERIALNO IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM MPI_COB_T_SERIAL_NO_HISTORY x
                                WHERE x.WORKSTATION = {{0}} AND x.SERIALNO = h.SERIALNO AND x.CREATEDON < {{1}})
                GROUP BY h.SERIALNO
            ) t
            GROUP BY {ShiftExpr("t.f")}, {HourExpr("t.f")}";
                        return RunAgg(sql, ws, s, e);
        }

        // Paint line: din ke andar DISTINCT by SERIALNO (har engine 1 baar). SQL GROUP BY.
        private Task<List<ShiftHourCount>> PaintAgg(DateTime s, DateTime e)
        {
            var sql = $@"
            SELECT {ShiftExpr("t.f")} AS Shift, {HourExpr("t.f")} AS Hour, COUNT(*) AS Cnt
            FROM (
                SELECT MIN(CREATEDON) AS f
                FROM MPI_COB_T_SERIAL_NO_HISTORY
                WHERE WORKSTATION = {{0}} AND CREATEDON >= {{1}} AND CREATEDON < {{2}} AND SERIALNO IS NOT NULL
                GROUP BY SERIALNO
            ) t
            GROUP BY {ShiftExpr("t.f")}, {HourExpr("t.f")}";
                        return RunAgg(sql, PAINT_LINE, s, e);
        }

        // Test Cell: har scan COUNT(*) (duplicates). SQL GROUP BY.
        private Task<List<ShiftHourCount>> TestCellAgg(DateTime s, DateTime e)
        {
            var sql = $@"
            SELECT {ShiftExpr("CREATEDON")} AS Shift, {HourExpr("CREATEDON")} AS Hour, COUNT(*) AS Cnt
            FROM COB_T_AMI_CAPTURE_LOG
            WHERE WORKSTATION = {{0}} AND CREATEDON >= {{1}} AND CREATEDON < {{2}}
            GROUP BY {ShiftExpr("CREATEDON")}, {HourExpr("CREATEDON")}";
                        return RunAgg(sql, TEST_CELL_WS, s, e);
        }

        // FES: OUTBOUND ⨝ SERIAL_NO, COUNT(*). SQL GROUP BY.
        private Task<List<ShiftHourCount>> FesAgg(DateTime s, DateTime e)
        {
            var sql = $@"
            SELECT {ShiftExpr("S.CREATEDON")} AS Shift, {HourExpr("S.CREATEDON")} AS Hour, COUNT(*) AS Cnt
            FROM MPI_COB_T_TRANSACTION_OUTBOUND S
            INNER JOIN MPI_COB_T_SERIAL_NO C ON C.WORKORDERNO = S.WIPJOBNO
            WHERE LEN(C.SERIALNO) = 8 AND C.STATUS IN (3,4) AND S.OVERALLSTATUS = 3
            AND S.CREATEDON >= {{0}} AND S.CREATEDON < {{1}}
            GROUP BY {ShiftExpr("S.CREATEDON")}, {HourExpr("S.CREATEDON")}";
                        return RunAgg(sql, s, e);
        }

        // Calendar range (min/max business day) - cache 2 min.
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

        // Selected din mein latest activity (cutoff) - "abhi tak" ka point. Yesterday se same-time compare ke liye.
        private async Task<DateTime?> MaxActivity(DateTime s, DateTime e)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var l = new List<DateTime>();
            foreach (var m in new[]
            {
                await db.SerialNoHistory.Where(x => x.CreatedOn >= s && x.CreatedOn < e).MaxAsync(x => (DateTime?)x.CreatedOn!.Value),
                await db.AmiCaptureLog.Where(x => x.CreatedOn >= s && x.CreatedOn < e).MaxAsync(x => (DateTime?)x.CreatedOn!.Value),
                await db.TransactionOutbound.Where(x => x.CreatedOn >= s && x.CreatedOn < e).MaxAsync(x => (DateTime?)x.CreatedOn!.Value),
            }) if (m != null) l.Add(m.Value);
            return l.Count == 0 ? (DateTime?)null : l.Max();
        }

        // % change (cur vs prev). prev=0 -> null (compare nahi ho sakta).
        private static int? Pct(int cur, int prev) =>
            prev > 0 ? (int)Math.Round((cur - prev) / (double)prev * 100.0) : (int?)null;

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

            // 5 metric aggregates PARALLEL - sab COUNT/GROUP BY SQL mein.
            var oldT = NewEngineAgg(OLD_LINE, startUtc, endUtc);
            var newT = NewEngineAgg(NEW_LINE, startUtc, endUtc);
            var tcT = TestCellAgg(startUtc, endUtc);
            var paintT = PaintAgg(startUtc, endUtc);
            var fesT = FesAgg(startUtc, endUtc);
            await Task.WhenAll(oldT, newT, tcT, paintT, fesT);

            var old = oldT.Result; var nw = newT.Result; var tc = tcT.Result;
            var pa = paintT.Result; var fe = fesT.Result;

            static int Tot(List<ShiftHourCount> a) => a.Sum(x => x.Cnt);
            static int Sh(List<ShiftHourCount> a, string s) => a.Where(x => x.Shift == s).Sum(x => x.Cnt);
            static int Hr(List<ShiftHourCount> a, int h) => a.Where(x => x.Hour == h).Sum(x => x.Cnt);

            bool hasData = Tot(old) + Tot(nw) + Tot(tc) + Tot(pa) + Tot(fe) > 0;

            object ShiftBlock(string s) => new
            {
                oldLine = Sh(old, s),
                newLine = Sh(nw, s),
                testCell = Sh(tc, s),
                paintLine = Sh(pa, s),
                fes = Sh(fe, s),
                shipped = 0
            };
            var shifts = new { A = ShiftBlock("A"), B = ShiftBlock("B"), C = ShiftBlock("C") };

            int[] order = { 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 0, 1, 2, 3, 4, 5 };
            var hourly = order.Select(h => new
            {
                hour = $"{h:00}:00",
                oldLine = Hr(old, h),
                newLine = Hr(nw, h),
                testCell = Hr(tc, h),
                paintLine = Hr(pa, h),
                fes = Hr(fe, h),
            }).ToList();

            // ---- Kal se compare (isi time tak) - stocks board jaisa % up/down ----
            object? compare = null;
            var cutoff = await MaxActivity(startUtc, endUtc);
            if (cutoff != null)
            {
                var elapsed = cutoff.Value - startUtc;             // is din kitna aage aaye
                var pStart = startUtc.AddDays(-1);
                var pEnd = pStart + elapsed;                       // kal isi time tak
                var pOld = NewEngineAgg(OLD_LINE, pStart, pEnd);
                var pNew = NewEngineAgg(NEW_LINE, pStart, pEnd);
                var pTc = TestCellAgg(pStart, pEnd);
                var pPa = PaintAgg(pStart, pEnd);
                var pFe = FesAgg(pStart, pEnd);
                await Task.WhenAll(pOld, pNew, pTc, pPa, pFe);
                compare = new
                {
                    oldLine = Pct(Tot(old), Tot(pOld.Result)),
                    newLine = Pct(Tot(nw), Tot(pNew.Result)),
                    testCell = Pct(Tot(tc), Tot(pTc.Result)),
                    paintLine = Pct(Tot(pa), Tot(pPa.Result)),
                    fes = Pct(Tot(fe), Tot(pFe.Result)),
                };
            }

            return Ok(new
            {
                productionDay = selected.ToString("yyyy-MM-dd"),
                hasData,
                minDate = minDay.ToString("yyyy-MM-dd"),
                maxDate = maxDay.ToString("yyyy-MM-dd"),
                kpis = new
                {
                    oldLine = Tot(old),
                    newLine = Tot(nw),
                    testCell = Tot(tc),
                    paintLine = Tot(pa),
                    fes = Tot(fe),
                    shipped = 0
                },
                compare,
                shifts,
                hourly
            });
        }

        // ===== Trends - DAILY = selected MONTH ke din, MONTHLY = SAARA history. 5 series. =====
        public class DayCount { public DateTime BizDay { get; set; } public int Cnt { get; set; } }
        public class MonthCount { public int Yr { get; set; } public int Mo { get; set; } public int Cnt { get; set; } }

        private async Task<List<DayCount>> RunDay(string sql, params object[] ps)
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Database.SqlQueryRaw<DayCount>(sql, ps).ToListAsync();
        }
        private async Task<List<MonthCount>> RunMonth(string sql, params object[] ps)
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Database.SqlQueryRaw<MonthCount>(sql, ps).ToListAsync();
        }

        // ---- DAILY (ek month - {1}=monthStart, {2}=monthEnd). Distinct serials/din (line), COUNT (test/fes). ----
        private const string DAY_LINE = @"SELECT CAST(DATEADD(MINUTE,-30,CREATEDON) AS DATE) AS BizDay, COUNT(DISTINCT SERIALNO) AS Cnt
FROM MPI_COB_T_SERIAL_NO_HISTORY WHERE WORKSTATION={0} AND CREATEDON>={1} AND CREATEDON<{2} AND SERIALNO IS NOT NULL
GROUP BY CAST(DATEADD(MINUTE,-30,CREATEDON) AS DATE)";
        private const string DAY_TEST = @"SELECT CAST(DATEADD(MINUTE,-30,CREATEDON) AS DATE) AS BizDay, COUNT(*) AS Cnt
FROM COB_T_AMI_CAPTURE_LOG WHERE WORKSTATION={0} AND CREATEDON>={1} AND CREATEDON<{2}
GROUP BY CAST(DATEADD(MINUTE,-30,CREATEDON) AS DATE)";
        private const string DAY_FES = @"SELECT CAST(DATEADD(MINUTE,-30,S.CREATEDON) AS DATE) AS BizDay, COUNT(*) AS Cnt
FROM MPI_COB_T_TRANSACTION_OUTBOUND S INNER JOIN MPI_COB_T_SERIAL_NO C ON C.WORKORDERNO=S.WIPJOBNO
WHERE LEN(C.SERIALNO)=8 AND C.STATUS IN(3,4) AND S.OVERALLSTATUS=3 AND S.CREATEDON>={0} AND S.CREATEDON<{1}
GROUP BY CAST(DATEADD(MINUTE,-30,S.CREATEDON) AS DATE)";

        // ---- MONTHLY (SAARA history - no date bound). Har saal/mahina. ----
        private const string MON_LINE = @"SELECT YEAR(DATEADD(MINUTE,-30,CREATEDON)) AS Yr, MONTH(DATEADD(MINUTE,-30,CREATEDON)) AS Mo, COUNT(DISTINCT SERIALNO) AS Cnt
FROM MPI_COB_T_SERIAL_NO_HISTORY WHERE WORKSTATION={0} AND SERIALNO IS NOT NULL
GROUP BY YEAR(DATEADD(MINUTE,-30,CREATEDON)), MONTH(DATEADD(MINUTE,-30,CREATEDON))";
        private const string MON_TEST = @"SELECT YEAR(DATEADD(MINUTE,-30,CREATEDON)) AS Yr, MONTH(DATEADD(MINUTE,-30,CREATEDON)) AS Mo, COUNT(*) AS Cnt
FROM COB_T_AMI_CAPTURE_LOG WHERE WORKSTATION={0}
GROUP BY YEAR(DATEADD(MINUTE,-30,CREATEDON)), MONTH(DATEADD(MINUTE,-30,CREATEDON))";
        private const string MON_FES = @"SELECT YEAR(DATEADD(MINUTE,-30,S.CREATEDON)) AS Yr, MONTH(DATEADD(MINUTE,-30,S.CREATEDON)) AS Mo, COUNT(*) AS Cnt
FROM MPI_COB_T_TRANSACTION_OUTBOUND S INNER JOIN MPI_COB_T_SERIAL_NO C ON C.WORKORDERNO=S.WIPJOBNO
WHERE LEN(C.SERIALNO)=8 AND C.STATUS IN(3,4) AND S.OVERALLSTATUS=3
GROUP BY YEAR(DATEADD(MINUTE,-30,S.CREATEDON)), MONTH(DATEADD(MINUTE,-30,S.CREATEDON))";

        // Daily = ek month ke din (cache per-month). monthEndUtc = agle month ka start.
        private async Task<object> DailyForMonth(DateTime mFirst)
        {
            var key = $"daily_{mFirst:yyyyMM}";
            if (_cache.TryGetValue(key, out object? c) && c != null) return c;

            var s = DayStartUtc(mFirst);                 // month day1 00:30 UTC
            var e = DayStartUtc(mFirst.AddMonths(1));     // next month day1 00:30 UTC
            var dOld = RunDay(DAY_LINE, OLD_LINE, s, e);
            var dNew = RunDay(DAY_LINE, NEW_LINE, s, e);
            var dPaint = RunDay(DAY_LINE, PAINT_LINE, s, e);
            var dTest = RunDay(DAY_TEST, TEST_CELL_WS, s, e);
            var dFes = RunDay(DAY_FES, s, e);
            await Task.WhenAll(dOld, dNew, dPaint, dTest, dFes);

            static Dictionary<DateTime, int> DM(List<DayCount> l) => l.ToDictionary(x => x.BizDay, x => x.Cnt);
            var doM = DM(dOld.Result); var dnM = DM(dNew.Result); var dpM = DM(dPaint.Result);
            var dtM = DM(dTest.Result); var dfM = DM(dFes.Result);
            var days = doM.Keys.Concat(dnM.Keys).Concat(dpM.Keys).Concat(dtM.Keys).Concat(dfM.Keys)
                .Distinct().OrderBy(d => d).ToList();
            var daily = days.Select(d => new
            {
                date = d.ToString("dd MMM"),
                oldLine = doM.GetValueOrDefault(d),
                newLine = dnM.GetValueOrDefault(d),
                testCell = dtM.GetValueOrDefault(d),
                paintLine = dpM.GetValueOrDefault(d),
                fes = dfM.GetValueOrDefault(d),
            }).ToList();
            _cache.Set(key, (object)daily, TimeSpan.FromMinutes(5));
            return daily;
        }

        // Monthly = saara history (cache once).
        private async Task<object> MonthlyAll()
        {
            if (_cache.TryGetValue("monthly", out object? c) && c != null) return c;

            var mOld = RunMonth(MON_LINE, OLD_LINE);
            var mNew = RunMonth(MON_LINE, NEW_LINE);
            var mPaint = RunMonth(MON_LINE, PAINT_LINE);
            var mTest = RunMonth(MON_TEST, TEST_CELL_WS);
            var mFes = RunMonth(MON_FES);
            await Task.WhenAll(mOld, mNew, mPaint, mTest, mFes);

            static Dictionary<(int, int), int> MM(List<MonthCount> l) => l.ToDictionary(x => (x.Yr, x.Mo), x => x.Cnt);
            var moM = MM(mOld.Result); var mnM = MM(mNew.Result); var mpM = MM(mPaint.Result);
            var mtM = MM(mTest.Result); var mfM = MM(mFes.Result);
            var months = moM.Keys.Concat(mnM.Keys).Concat(mpM.Keys).Concat(mtM.Keys).Concat(mfM.Keys)
                .Distinct().OrderBy(k => k.Item1).ThenBy(k => k.Item2).ToList();
            var monthly = months.Select(k => new
            {
                month = new DateTime(k.Item1, k.Item2, 1).ToString("MMM yyyy"),
                oldLine = moM.GetValueOrDefault(k),
                newLine = mnM.GetValueOrDefault(k),
                testCell = mtM.GetValueOrDefault(k),
                paintLine = mpM.GetValueOrDefault(k),
                fes = mfM.GetValueOrDefault(k),
            }).ToList();
            _cache.Set("monthly", (object)monthly, TimeSpan.FromMinutes(5));
            return monthly;
        }

        // GET /api/Dashboard/trends?month=2026-06  -> daily (us month ke din) + monthly (saara history).
        [HttpGet("trends")]
        public async Task<IActionResult> Trends([FromQuery] string? month = null)
        {
            var range = await GetRangeAsync();
            if (range == null) return Ok(new { daily = Array.Empty<object>(), monthly = Array.Empty<object>() });

            DateTime mFirst = (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month + "-01", out var mp))
                ? new DateTime(mp.Year, mp.Month, 1)
                : new DateTime(range.Value.max.Year, range.Value.max.Month, 1);

            var daily = await DailyForMonth(mFirst);
            var monthly = await MonthlyAll();
            return Ok(new { daily, monthly });
        }
    }
}
