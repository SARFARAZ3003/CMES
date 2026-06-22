using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Caching.Memory;

namespace CMES.Services
{
    // Cycle time (sec/engine) per line. SOURCE = Oracle DB (TCL_T_CYCLETIME), admin-managed, READ-ONLY.
    // Hum sirf FETCH + DISPLAY karte hain - change sirf Oracle admin kar sakta.
    // ORACLE_DB connection string khaali ho (local dev) to appsettings "CycleTimes" defaults use hote hain (fallback).
    // Real deploy: bas ORACLE_DB string daalo -> Oracle se aane lagega. KOI code change nahi.
    public class CycleTimeService
    {
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CycleTimeService> _logger;

        public CycleTimeService(IConfiguration config, IMemoryCache cache, ILogger<CycleTimeService> logger)
        {
            _config = config; _cache = cache; _logger = logger;
        }

        // Humare line keys (dashboard) - inhi ko Oracle ki LINE value se map karte hain.
        private static readonly string[] Keys = { "OldLine", "NewLine", "TestCell", "PaintLine" };
        private static readonly Dictionary<string, int> Hardcoded = new()
        {
            ["OldLine"] = 150, ["NewLine"] = 140, ["TestCell"] = 180, ["PaintLine"] = 180
        };

        // Fallback: appsettings "CycleTimes:<key>", warna hardcoded default.
        private int Fallback(string key) =>
            _config.GetValue<int?>($"CycleTimes:{key}") ?? Hardcoded.GetValueOrDefault(key, 150);

        // Cache 5 min - cycle time baar-baar nahi badalta; har overview/refresh pe Oracle hit na ho.
        public async Task<Dictionary<string, int>> GetAsync()
        {
            if (_cache.TryGetValue("cycletimes", out Dictionary<string, int>? cached) && cached != null)
                return cached;

            // Pehle fallback values bhar do (Oracle na ho ya fail ho to yahi).
            var result = Keys.ToDictionary(k => k, Fallback);

            var conn = _config.GetConnectionString("ORACLE_DB");
            if (!string.IsNullOrWhiteSpace(conn))
            {
                try
                {
                    // Table/column naam config se (default = sir ke diye TCL_T_CYCLETIME / LINE / CYCLETIME).
                    var table = _config["CycleTimeSource:Table"] ?? "TCL_T_CYCLETIME";
                    var lineCol = _config["CycleTimeSource:LineColumn"] ?? "LINE";
                    var valCol = _config["CycleTimeSource:ValueColumn"] ?? "CYCLETIME";

                    // Oracle ki LINE value -> humara key. Config "CycleTimeSource:LineMap" se (reverse).
                    var lineMap = _config.GetSection("CycleTimeSource:LineMap").GetChildren()
                        .ToDictionary(x => (x.Value ?? "").Trim().ToUpperInvariant(), x => x.Key);

                    await using var oc = new OracleConnection(conn);
                    await oc.OpenAsync();
                    await using var cmd = oc.CreateCommand();
                    cmd.CommandText = $"SELECT {lineCol}, {valCol} FROM {table}";
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        var lineVal = rd[0]?.ToString()?.Trim().ToUpperInvariant() ?? "";
                        if (rd[1] == DBNull.Value) continue;
                        var sec = Convert.ToInt32(rd.GetValue(1));
                        if (lineMap.TryGetValue(lineVal, out var key) && sec > 0)
                            result[key] = sec;
                    }
                    _logger.LogInformation("[CYCLE-OK] Oracle TCL_T_CYCLETIME se fetch -> {Vals}",
                        string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value}")));
                }
                catch (Exception ex)
                {
                    // Oracle fail -> fallback values (already result me) se chalta rahe, app crash na ho.
                    _logger.LogError(ex, "[CYCLE-FALLBACK] Oracle cycle-time fetch FAIL -> appsettings defaults. {Msg}",
                        ex.GetBaseException().Message);
                }
            }

            _cache.Set("cycletimes", result, TimeSpan.FromMinutes(5));
            return result;
        }
    }
}
