using Oracle.ManagedDataAccess.Client;

namespace CMES.Services
{
    // Shipped (dispatched engines) per shift. SOURCE = Oracle DB, table TCL_T_DISPATCHSHIFT.
    // Shipped[shift] = SUM(DESP_QTY) WHERE DATEX = <production day> AND SHIFT = <A/B/C>.
    // ORACLE_DB khaali (local dev) -> sab 0 (koi SQL Server source nahi). Real Oracle ka string daalo -> aane lagega, koi code change nahi.
    public class DispatchService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<DispatchService> _logger;
        public DispatchService(IConfiguration config, ILogger<DispatchService> logger)
        {
            _config = config; _logger = logger;
        }

        // date = production day (DATEX se match). Returns per-shift shipped {A,B,C} (jo na mile = 0).
        public async Task<Dictionary<string, int>> GetShippedAsync(DateTime date)
        {
            var result = new Dictionary<string, int> { ["A"] = 0, ["B"] = 0, ["C"] = 0 };

            var conn = _config.GetConnectionString("ORACLE_DB");
            if (string.IsNullOrWhiteSpace(conn)) return result;   // local fallback: 0

            try
            {
                // Table/column naam config se (default = sir ke diye TCL_T_DISPATCHSHIFT / DATEX / SHIFT / DESP_QTY).
                var table = _config["DispatchSource:Table"] ?? "TCL_T_DISPATCHSHIFT";
                var dateCol = _config["DispatchSource:DateColumn"] ?? "DATEX";
                var shiftCol = _config["DispatchSource:ShiftColumn"] ?? "SHIFT";
                var qtyCol = _config["DispatchSource:QtyColumn"] ?? "DESP_QTY";

                await using var oc = new OracleConnection(conn);
                await oc.OpenAsync();
                await using var cmd = oc.CreateCommand();
                cmd.BindByName = true;
                // TRUNC(DATEX) se time-part ignore (DATE column). Ek query me teeno shift ka SUM.
                cmd.CommandText = $"SELECT {shiftCol}, SUM({qtyCol}) FROM {table} " +
                                  $"WHERE TRUNC({dateCol}) = TO_DATE(:d,'YYYY-MM-DD') GROUP BY {shiftCol}";
                cmd.Parameters.Add(new OracleParameter("d", date.ToString("yyyy-MM-dd")));

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var sh = rd[0]?.ToString()?.Trim().ToUpperInvariant() ?? "";
                    if (rd[1] == DBNull.Value) continue;
                    var qty = Convert.ToInt32(rd.GetValue(1));
                    if (result.ContainsKey(sh)) result[sh] = qty;
                }
                _logger.LogInformation("[SHIPPED-OK] {Date} -> A={A} B={B} C={C}",
                    date.ToString("yyyy-MM-dd"), result["A"], result["B"], result["C"]);
            }
            catch (Exception ex)
            {
                // Oracle fail -> 0 (app crash na ho).
                _logger.LogError(ex, "[SHIPPED-FAIL] Oracle dispatch fetch FAIL -> 0. {Msg}",
                    ex.GetBaseException().Message);
            }
            return result;
        }
    }
}
