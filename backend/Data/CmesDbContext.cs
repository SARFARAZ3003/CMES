using Microsoft.EntityFrameworkCore;
using CMES.Models;

namespace CMES.Data
{
    // EF Core context for the FlexNet production DB.
    // Sirf real assembly history table chahiye - dashboard isi pe banta hain.
    public class CmesDbContext : DbContext
    {
        public CmesDbContext(DbContextOptions<CmesDbContext> options) : base(options) { }

        // Real production data: dbo.MPI_COB_T_SERIAL_NO_HISTORY
        public DbSet<SerialNoHistory> SerialNoHistory => Set<SerialNoHistory>();
    }
}
