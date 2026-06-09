namespace CMES.Models
{
    // Ek shift / din ka production summary record
    public class Production
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Shift { get; set; } = "";   // A / B / C
        public int OldLine { get; set; }
        public int NewLine { get; set; }
        public int TestCycle { get; set; }
        public int Fes { get; set; }
        public int Dispatched { get; set; }
        public int TestOK { get; set; }
    }
}
