using Microsoft.EntityFrameworkCore;
using CMES.Models;

namespace CMES.Data
{
    public class CmesDbContext : DbContext
    {
        public CmesDbContext(DbContextOptions<CmesDbContext> options) : base(options) { }

        // Production data — used by ProductionController
        public DbSet<Production> Productions => Set<Production>();

        // Raw assembly history — reference table for future Production Report queries
        // following the WipController SQL-first pattern
        public DbSet<SerialNoHistory> SerialNoHistory => Set<SerialNoHistory>();
    }
}
