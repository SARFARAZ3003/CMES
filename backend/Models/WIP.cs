namespace CMES.Models
{
    // Work-In-Progress count ek location pe
    public class WIP
    {
        public int Id { get; set; }
        public string Location { get; set; } = "";  // Quality Dock, Paint Line, ...
        public int Count { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
