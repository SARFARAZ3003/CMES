using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CMES.Models
{
    // Real production table: dbo.MPI_COB_T_SERIAL_NO_HISTORY (FlexNet DB)
    // Read-only - sirf zaroori columns map kiye hain display ke liye.
    [Keyless]
    [Table("MPI_COB_T_SERIAL_NO_HISTORY")]
    public class SerialNoHistory
    {
        [Column("ID")]             public double? Id { get; set; }
        [Column("PRODUCTID")]      public double? ProductId { get; set; }
        [Column("SERIALNO")]       public string? SerialNo { get; set; }
        [Column("LOTNO")]          public string? LotNo { get; set; }
        [Column("WORKORDERNO")]    public string? WorkOrderNo { get; set; }
        [Column("WORKSTATION")]    public string? Workstation { get; set; }
        [Column("STATUS")]         public double? Status { get; set; }
        [Column("PREVIOUSSTATUS")] public double? PreviousStatus { get; set; }
        [Column("LOCATION")]       public string? Location { get; set; }
        [Column("APPLICATION")]    public string? Application { get; set; }
        [Column("CREATEDON")]      public DateTime? CreatedOn { get; set; }
        [Column("CREATEDBY")]      public string? CreatedBy { get; set; }
    }
}
