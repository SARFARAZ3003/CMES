using Microsoft.EntityFrameworkCore;
using CMES.Models;

namespace CMES.Data
{
    // Authorization ke liye ALAG database (AUTH_DB connection).
    // Production data (CmesDbContext) se bilkul alag - sir ke setup mein CMES_USERS
    // ek doosre DB mein hain. Locally dono FlexNet pe point karte hain.
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        // dbo.CMES_USERS
        public DbSet<CmesUser> Users => Set<CmesUser>();
    }
}
