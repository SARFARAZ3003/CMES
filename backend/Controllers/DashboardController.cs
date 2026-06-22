using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using CMES.Data;
using CMES.Services;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "CmesUser")]   // sirf active CMES user dashboard data dekh sakta hain
    public class DashboardController : ControllerBase
    {
        // Factory: har query apna context banata hain -> parallel chal sakti hain.
        private readonly IDbContextFactory<CmesDbContext> _factory;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly ILogger<DashboardController> _logger;
        private readonly CycleTimeService _cycle;   // cycle-time Oracle se (ya fallback)
        private readonly DispatchService _dispatch; // shipped (dispatch) Oracle se (ya 0)
        public DashboardController(IDbContextFactory<CmesDbContext> factory, IMemoryCache cache, IConfiguration config, ILogger<DashboardController> logger, CycleTimeService cycle, DispatchService dispatch)
        {
            _factory = factory;
            _cache = cache;
            _config = config;
            _logger = logger;
            _cycle = cycle;
            _dispatch = dispatch;
        }

        // Parallel tasks me se kaun-kaun FAIL hui - naam + ASLI SQL error (console pe exact pinpoint).
        private static string FailedQueries(params (string name, Task task)[] tasks) =>
            string.Join("  ||  ", tasks.Where(t => t.task.IsFaulted)
                .Select(t => $"{t.name} -> {t.task.Exception?.GetBaseException().Message}"));

        // ---- Line mapping (sir ke hisaab se) ----
        private const string OLD_LINE = "23800";     // MPI_COB_T_SERIAL_NO_HISTORY, NOT EXISTS (naya engine)
        private const string NEW_LINE = "33200";     // ditto
        private const string PAINT_LINE = "52000";   // ditto par DISTINCT serials per day (dedup)
        private const string TEST_CELL_WS = "40200"; // MPI_COB_T_AMI_CAPTURE_LOG, COUNT(*) (duplicates)
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

        // ===== Cycle-time PLAN (sir: % plan ke hisaab se, cycle-time se - previous day se NAHI) =====
        // Per-line cycle time (sec/engine) ab Oracle (TCL_T_CYCLETIME) se aata - CycleTimeService dekho.

        // Breaks (IST clock) - plan production me ye time nahi gina jaata (sir ne diye).
        private static readonly (TimeSpan start, TimeSpan end)[] Breaks =
        {
            (new(7,45,0),  new(8,0,0)),
            (new(10,30,0), new(11,0,0)),
            (new(12,45,0), new(13,0,0)),
            (new(16,45,0), new(17,0,0)),
            (new(19,0,0),  new(19,30,0)),
            (new(20,45,0), new(21,0,0)),
            (new(1,0,0),   new(1,30,0)),
            (new(4,0,0),   new(6,0,0)),
        };

        // Production day [dayStart 06:00 IST, effNow] me break seconds (overlap). Clock>=06:00 -> us din, warna agle din.
        private static double BreakSeconds(DateTime dayStartIst, DateTime effNowIst)
        {
            double sec = 0;
            foreach (var b in Breaks)
            {
                var date = (b.start >= AStart ? dayStartIst.Date : dayStartIst.Date.AddDays(1));
                var bs = date + b.start; var be = date + b.end;
                var ov = Math.Min(be.Ticks, effNowIst.Ticks) - Math.Max(bs.Ticks, dayStartIst.Ticks);
                if (ov > 0) sec += TimeSpan.FromTicks(ov).TotalSeconds;
            }
            return sec;
        }

        // Plan count = working seconds / cycle (round). Cycle/working<=0 -> 0.
        private static int PlanCount(double workSec, int cyc) => cyc > 0 && workSec > 0 ? (int)Math.Round(workSec / cyc) : 0;
        // % actual vs plan: signed (actual-plan)/plan, 2 decimals. plan=0 -> null.
        private static double? PlanPct(int actual, int plan) =>
            plan > 0 ? Math.Round((actual - plan) / (double)plan * 100.0, 2) : (double?)null;

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

        // Paint = WS 52000, us din ke DISTINCT engines (serial dedup - duplicate scan ek hi baar).
        // Har serial ka 1 timestamp (min) rakha taaki shift/hour bucketing kaam kare.
        private async Task<List<DateTime>> PaintScans(DateTime startUtc, DateTime endUtc)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var rows = await db.SerialNoHistory.AsNoTracking()
                .Where(h => h.Workstation == PAINT_LINE && h.CreatedOn >= startUtc && h.CreatedOn < endUtc && h.SerialNo != null)
                .Select(h => new { h.SerialNo, On = h.CreatedOn!.Value })
                .ToListAsync();
            return rows.GroupBy(r => r.SerialNo).Select(g => g.Min(x => x.On)).ToList();
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

            try
            {
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CMES-DB-ERROR] GetRangeAsync (calendar min/max on HIST/AMI/OUTBOUND) FAILED -> {Msg}", ex.GetBaseException().Message);
                throw;
            }
        }

        // GET /api/Dashboard/overview?date=2026-06-10
        // Cycle-time Oracle se (read-only) - UI se nahi aata. (live refresh isi ko hit karta hain)
        [HttpGet("overview")]
        public async Task<IActionResult> Overview([FromQuery] string? date = null)
        {
            (DateTime min, DateTime max)? range;
            try { range = await GetRangeAsync(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CMES-DB-ERROR] /overview -> calendar range query fail. detail: {Msg}", ex.GetBaseException().Message);
                return StatusCode(500, new { error = "Calendar range (min/max date) query failed", where = "GetRangeAsync", detail = ex.GetBaseException().Message });
            }
            if (range == null) return Ok(new { productionDay = (string?)null, message = "No data" });
            var (minDay, maxDay) = range.Value;

            var selected = maxDay;
            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsed))
                selected = parsed.Date;

            var startUtc = DayStartUtc(selected);
            var endUtc = startUtc.AddDays(1);

            // Shipped (Oracle TCL_T_DISPATCHSHIFT) ko metrics ke saath PARALLEL chalu kar do (alag Oracle conn).
            var shippedT = _dispatch.GetShippedAsync(selected);

            // 5 metric queries SINGLE-PHASE parallel (har ek apna context). Plan% cycle-time se hota hai (DB query nahi),
            // isliye ab koi prev-day compare query nahi - sirf 5.
            var oldT = NewEngineScans(OLD_LINE, startUtc, endUtc);
            var newT = NewEngineScans(NEW_LINE, startUtc, endUtc);
            var tcT = TestCellScans(startUtc, endUtc);
            var paintT = PaintScans(startUtc, endUtc);
            var fesT = FesScans(startUtc, endUtc);
            try { await Task.WhenAll(oldT, newT, tcT, paintT, fesT); }
            catch
            {
                var failed = FailedQueries(("OldLine", oldT), ("NewLine", newT), ("TestCell", tcT), ("PaintLine", paintT), ("FES", fesT));
                _logger.LogError("[CMES-DB-ERROR] /overview date={Date} -> metric query FAIL: {Failed}", selected.ToString("yyyy-MM-dd"), failed);
                return StatusCode(500, new { error = "Overview metric query failed", date = selected.ToString("yyyy-MM-dd"), where = failed });
            }

            var oldIst = oldT.Result.Select(ToIst).ToList();
            var newIst = newT.Result.Select(ToIst).ToList();
            var tcIst = tcT.Result.Select(ToIst).ToList();
            var paintIst = paintT.Result.Select(ToIst).ToList();
            var fesIst = fesT.Result.Select(ToIst).ToList();

            bool hasData = oldIst.Count + newIst.Count + tcIst.Count + paintIst.Count + fesIst.Count > 0;

            // Shipped per shift (Oracle se; local pe sab 0). Total = teeno ka jod.
            var shipped = await shippedT;
            int shippedTotal = shipped.Values.Sum();

            object ShiftBlock(string s) => new
            {
                oldLine = oldIst.Count(x => ShiftOf(x) == s),
                newLine = newIst.Count(x => ShiftOf(x) == s),
                testCell = tcIst.Count(x => ShiftOf(x) == s),
                paintLine = paintIst.Count(x => ShiftOf(x) == s),
                fes = fesIst.Count(x => ShiftOf(x) == s),
                shipped = shipped.GetValueOrDefault(s)
            };
            var shifts = new { A = ShiftBlock("A"), B = ShiftBlock("B"), C = ShiftBlock("C") };

            // Half-hour buckets (IST), production day 06:00 -> 05:30. Shift exact :30 boundary pe (ShiftOf).
            var hourly = Enumerable.Range(0, 48).Select(i =>
            {
                int tot = (6 * 60 + i * 30) % (24 * 60);
                int hh = tot / 60, mm = tot % 60;
                bool half = mm == 30;
                int Cnt(List<DateTime> L) => L.Count(x => x.Hour == hh && (x.Minute >= 30) == half);
                return new
                {
                    time = $"{hh:00}:{mm:00}",
                    shift = ShiftOf(new DateTime(2000, 1, 1, hh, mm, 0)),
                    oldLine = Cnt(oldIst),
                    newLine = Cnt(newIst),
                    testCell = Cnt(tcIst),
                    paintLine = Cnt(paintIst),
                    fes = Cnt(fesIst),
                };
            }).ToList();

            // ---- Plan vs actual (cycle-time + breaks) - sir: % plan se, previous day se NAHI. Sirf O/N/T/P ----
            // Whole-day basis: din-start (06:00 IST) se ab tak ka working time (breaks minus) / per-line cycle.
            var dayStartIst = ToIst(startUtc);
            var nowIst = ToIst(DateTime.UtcNow);
            var dayEndIst = dayStartIst.AddDays(1);
            var effNow = nowIst < dayStartIst ? dayStartIst : (nowIst > dayEndIst ? dayEndIst : nowIst);
            var workSec = (effNow - dayStartIst).TotalSeconds - BreakSeconds(dayStartIst, effNow);

            // Cycle = Oracle (TCL_T_CYCLETIME) se, admin-managed read-only. Local pe fallback defaults. Plan inhi se banta.
            var cyc = await _cycle.GetAsync();
            int cOld = cyc["OldLine"];
            int cNew = cyc["NewLine"];
            int cTest = cyc["TestCell"];
            int cPaint = cyc["PaintLine"];
            int oPlan = PlanCount(workSec, cOld);
            int nPlan = PlanCount(workSec, cNew);
            int tPlan = PlanCount(workSec, cTest);
            int pPlan = PlanCount(workSec, cPaint);
            object? plan = hasData ? new
            {
                oldLine = new { target = oPlan, pct = PlanPct(oldIst.Count, oPlan) },
                newLine = new { target = nPlan, pct = PlanPct(newIst.Count, nPlan) },
                testCell = new { target = tPlan, pct = PlanPct(tcIst.Count, tPlan) },
                paintLine = new { target = pPlan, pct = PlanPct(paintIst.Count, pPlan) },
            } : null;

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
                    shipped = shippedTotal
                },
                plan,
                cycle = new { oldLine = cOld, newLine = cNew, testCell = cTest, paintLine = cPaint },
                shifts,
                hourly
            });
        }

        // ===== Trends (daily + monthly) - heavy aggregate, cache 5 min =====
        public class DayCount { public DateTime BizDay { get; set; } public int Cnt { get; set; } }

        // Naya engine per-din. Year-bound: sirf cutoff (1 Jan) ke baad first-scan wale.
        // Tie-safe: MIN-per-serial rakha (NOT EXISTS < CREATEDON duplicate timestamp pe double-count karta).
        // Bounded: pehle window me dikhe serials nikalo (CREATEDON >= cutoff), fir unka global-first MIN; HAVING se confirm first-ever bhi window me ho.
        private async Task<List<DayCount>> PerDayNewEngines(string ws, DateTime startUtc, DateTime endUtc)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var sql = @"
            SELECT CAST(DATEADD(MINUTE,-30, f.FirstOn) AS DATE) AS BizDay, COUNT(*) AS Cnt
            FROM (
                SELECT h.SERIALNO, MIN(h.CREATEDON) AS FirstOn
                FROM MPI_COB_T_SERIAL_NO_HISTORY h
                WHERE h.WORKSTATION = {0}
                  AND h.SERIALNO IN (
                      SELECT w.SERIALNO FROM MPI_COB_T_SERIAL_NO_HISTORY w
                      WHERE w.WORKSTATION = {0} AND w.SERIALNO IS NOT NULL AND w.CREATEDON >= {1} AND w.CREATEDON < {2})
                GROUP BY h.SERIALNO
                HAVING MIN(h.CREATEDON) >= {1} AND MIN(h.CREATEDON) < {2}
            ) f
            GROUP BY CAST(DATEADD(MINUTE,-30, f.FirstOn) AS DATE)
            ORDER BY BizDay";
            return await db.Database.SqlQueryRaw<DayCount>(sql, ws, startUtc, endUtc).ToListAsync();
        }

        private async Task<List<DayCount>> PerDayPaint(DateTime startUtc, DateTime endUtc)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var sql = @"
            SELECT CAST(DATEADD(MINUTE,-30, CREATEDON) AS DATE) AS BizDay, COUNT(DISTINCT SERIALNO) AS Cnt
            FROM MPI_COB_T_SERIAL_NO_HISTORY
            WHERE WORKSTATION = {0} AND CREATEDON >= {1} AND CREATEDON < {2}
            GROUP BY CAST(DATEADD(MINUTE,-30, CREATEDON) AS DATE)
            ORDER BY BizDay";
            return await db.Database.SqlQueryRaw<DayCount>(sql, PAINT_LINE, startUtc, endUtc).ToListAsync();
        }

        private async Task<List<DayCount>> PerDayTestCell(DateTime startUtc, DateTime endUtc)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var sql = @"
            SELECT CAST(DATEADD(MINUTE,-30, CREATEDON) AS DATE) AS BizDay, COUNT(*) AS Cnt
            FROM MPI_COB_T_AMI_CAPTURE_LOG
            WHERE WORKSTATION = {0} AND CREATEDON >= {1} AND CREATEDON < {2}
            GROUP BY CAST(DATEADD(MINUTE,-30, CREATEDON) AS DATE)
            ORDER BY BizDay";
            return await db.Database.SqlQueryRaw<DayCount>(sql, TEST_CELL_WS, startUtc, endUtc).ToListAsync();
        }

        private async Task<List<DayCount>> PerDayFes(DateTime startUtc, DateTime endUtc)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var sql = @"
            SELECT CAST(DATEADD(MINUTE,-30, S.CREATEDON) AS DATE) AS BizDay, COUNT(*) AS Cnt
            FROM MPI_COB_T_TRANSACTION_OUTBOUND S
            INNER JOIN MPI_COB_T_SERIAL_NO C ON C.WORKORDERNO = S.WIPJOBNO
            WHERE LEN(C.SERIALNO) = 8 AND C.STATUS IN (3,4) AND S.OVERALLSTATUS = 3 AND S.CREATEDON >= {0} AND S.CREATEDON < {1}
            GROUP BY CAST(DATEADD(MINUTE,-30, S.CREATEDON) AS DATE)
            ORDER BY BizDay";
            return await db.Database.SqlQueryRaw<DayCount>(sql, startUtc, endUtc).ToListAsync();
        }

        // GET /api/Dashboard/trends?year=2026  -> us saal ka daily (last 30) + monthly. Default = current year.
        // Ek time pe SIRF ek saal -> hamesha bounded (CREATEDON index seek), kabhi poora 50M scan nahi.
        [HttpGet("trends")]
        public async Task<IActionResult> Trends([FromQuery] int? year = null)
        {
            var y = year ?? ToIst(DateTime.UtcNow).Year;
            var cacheKey = $"trends_{y}";                  // per-year cache (2026 / 2023 alag).
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null)
                return Ok(cached);

            var startUtc = DayStartUtc(new DateTime(y, 1, 1));      // us saal 1 Jan
            var endUtc = DayStartUtc(new DateTime(y + 1, 1, 1));    // agle saal 1 Jan (exclusive)

            // 5 per-day queries PARALLEL (har ek apna context - factory) -> total = sabse dheere wali, sum nahi.
            var oldTask = PerDayNewEngines(OLD_LINE, startUtc, endUtc);
            var newTask = PerDayNewEngines(NEW_LINE, startUtc, endUtc);
            var paintTask = PerDayPaint(startUtc, endUtc);
            var tcTask = PerDayTestCell(startUtc, endUtc);
            var fesTask = PerDayFes(startUtc, endUtc);
            try { await Task.WhenAll(oldTask, newTask, paintTask, tcTask, fesTask); }
            catch
            {
                var failed = FailedQueries(("PerDayOld", oldTask), ("PerDayNew", newTask), ("PerDayPaint", paintTask), ("PerDayTestCell", tcTask), ("PerDayFes", fesTask));
                _logger.LogError("[CMES-DB-ERROR] /trends year={Year} -> per-day query FAIL: {Failed}", y, failed);
                return StatusCode(500, new { error = "Trends per-day query failed", year = y, where = failed });
            }

            var oldMap = oldTask.Result.ToDictionary(x => x.BizDay, x => x.Cnt);
            var newMap = newTask.Result.ToDictionary(x => x.BizDay, x => x.Cnt);
            var paintMap = paintTask.Result.ToDictionary(x => x.BizDay, x => x.Cnt);
            var tcMap = tcTask.Result.ToDictionary(x => x.BizDay, x => x.Cnt);
            var fesMap = fesTask.Result.ToDictionary(x => x.BizDay, x => x.Cnt);

            var days = oldMap.Keys.Concat(newMap.Keys).Concat(paintMap.Keys)
                .Concat(tcMap.Keys).Concat(fesMap.Keys)
                .Distinct().OrderBy(d => d).ToList();

            // Daily = us saal ke 1 Jan -> aakhri data-din (continuous; data na ho to 0).
            // Poora saal isliye taaki frontend selected-date ke kisi bhi month ko dikha sake.
            var daily = new List<object>();
            if (days.Count > 0)
            {
                var maxDay = days[^1];                                   // ascending sorted
                var yearStart = new DateTime(y, 1, 1);
                for (var d = yearStart; d <= maxDay; d = d.AddDays(1))
                    daily.Add(new
                    {
                        date = d.ToString("dd MMM"),
                        oldLine = oldMap.GetValueOrDefault(d),
                        newLine = newMap.GetValueOrDefault(d),
                        testCell = tcMap.GetValueOrDefault(d),
                        paintLine = paintMap.GetValueOrDefault(d),
                        fes = fesMap.GetValueOrDefault(d),
                    });
            }

            var monthly = days.GroupBy(d => new { d.Year, d.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    oldLine = g.Sum(d => oldMap.GetValueOrDefault(d)),
                    newLine = g.Sum(d => newMap.GetValueOrDefault(d)),
                    testCell = g.Sum(d => tcMap.GetValueOrDefault(d)),
                    paintLine = g.Sum(d => paintMap.GetValueOrDefault(d)),
                    fes = g.Sum(d => fesMap.GetValueOrDefault(d)),
                }).ToList();

            var result = new { year = y, daily, monthly };
            _cache.Set(cacheKey, (object)result, TimeSpan.FromMinutes(5));
            return Ok(result);
        }
    }
}
