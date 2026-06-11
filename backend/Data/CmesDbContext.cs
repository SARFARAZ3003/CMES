using Microsoft.EntityFrameworkCore;
using CMES.Models;

namespace CMES.Data
{
    // EF Core context for the FlexNet production DB. Sab read-only history/log tables.
    public class CmesDbContext : DbContext
    {
        public CmesDbContext(DbContextOptions<CmesDbContext> options) : base(options) { }

        // Old/New/Paint line scans: dbo.MPI_COB_T_SERIAL_NO_HISTORY
        public DbSet<SerialNoHistory> SerialNoHistory => Set<SerialNoHistory>();

        // Test Cell scans: dbo.COB_T_AMI_CAPTURE_LOG
        public DbSet<AmiCaptureLog> AmiCaptureLog => Set<AmiCaptureLog>();

        // FES: dbo.MPI_COB_T_TRANSACTION_OUTBOUND  ⨝  dbo.MPI_COB_T_SERIAL_NO
        public DbSet<TransactionOutbound> TransactionOutbound => Set<TransactionOutbound>();
        public DbSet<SerialMaster> SerialNo => Set<SerialMaster>();
    }
}
