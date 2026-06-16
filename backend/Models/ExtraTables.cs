using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CMES.Models
{
    // Test Cell scans: dbo.MPI_COB_T_AMI_CAPTURE_LOG (workstation 40200). Read-only subset.
    [Keyless]
    [Table("MPI_COB_T_AMI_CAPTURE_LOG")]
    public class AmiCaptureLog
    {
        [Column("WORKSTATION")] public string? Workstation { get; set; }
        [Column("SERIALNO")]    public string? SerialNo { get; set; }
        [Column("CREATEDON")]   public DateTime? CreatedOn { get; set; }
    }

    // Serial master: dbo.MPI_COB_T_SERIAL_NO (FES join ke liye). Read-only subset.
    [Keyless]
    [Table("MPI_COB_T_SERIAL_NO")]
    public class SerialMaster
    {
        [Column("SERIALNO")]    public string? SerialNo { get; set; }
        [Column("WORKORDERNO")] public string? WorkOrderNo { get; set; }
        [Column("STATUS")]      public double? Status { get; set; }
        [Column("CREATEDON")]   public DateTime? CreatedOn { get; set; }
    }

    // Outbound transactions: dbo.MPI_COB_T_TRANSACTION_OUTBOUND (FES). Read-only subset.
    [Keyless]
    [Table("MPI_COB_T_TRANSACTION_OUTBOUND")]
    public class TransactionOutbound
    {
        [Column("WIPJOBNO")]      public string? WipJobNo { get; set; }
        [Column("SERIALNO")]      public string? SerialNo { get; set; }
        [Column("OVERALLSTATUS")] public double? OverallStatus { get; set; }
        [Column("CREATEDON")]     public DateTime? CreatedOn { get; set; }
    }
}
