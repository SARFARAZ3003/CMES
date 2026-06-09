namespace CMES.Models
{
    // Employee jo plant mein kaam karta hain (operator, supervisor)
    public class Employee
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";       // e.g. OD741
        public string Name { get; set; } = "";
        public string Department { get; set; } = "";  // e.g. Production
        public string Shift { get; set; } = "";       // A / B / C
    }
}
