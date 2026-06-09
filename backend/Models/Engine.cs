namespace CMES.Models
{
    // Ek engine unit jo line pe banta hain
    public class Engine
    {
        public int Id { get; set; }
        public string EngineNo { get; set; } = "";    // unique serial
        public string Model { get; set; } = "";       // model / variant name
        public string Line { get; set; } = "";        // Old Line / New Line
        public string Location { get; set; } = "";    // current WIP location
        public string Status { get; set; } = "";      // WIP / FES / TestOK / Dispatched
        public string Shift { get; set; } = "";        // A / B / C
        public DateTime CreatedAt { get; set; }
    }
}
