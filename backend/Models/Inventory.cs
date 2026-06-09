namespace CMES.Models
{
    // Store / inventory item ka stock
    public class Inventory
    {
        public int Id { get; set; }
        public string PartNo { get; set; } = "";
        public string PartName { get; set; } = "";
        public string Category { get; set; } = "";   // Raw / Component / Consumable
        public int InStock { get; set; }
        public int MinLevel { get; set; }             // reorder level
        public string Unit { get; set; } = "";        // pcs / kg / ltr
        public string Status { get; set; } = "";      // OK / Low / Out
    }
}
