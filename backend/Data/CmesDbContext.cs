using Microsoft.EntityFrameworkCore;
using CMES.Models;

namespace CMES.Data
{
    // EF Core context - SQL Server (CMES_DB) ke saath baad mein connect karega.
    // Abhi controllers mock data return karte hain, par config ready hain.
    public class CmesDbContext : DbContext
    {
        public CmesDbContext(DbContextOptions<CmesDbContext> options) : base(options) { }

        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Engine> Engines => Set<Engine>();
        public DbSet<Production> Productions => Set<Production>();
        public DbSet<WIP> WIPs => Set<WIP>();
        public DbSet<Inventory> Inventories => Set<Inventory>();

        // Real production data (FlexNet DB)
        public DbSet<SerialNoHistory> SerialNoHistory => Set<SerialNoHistory>();
    }
}
