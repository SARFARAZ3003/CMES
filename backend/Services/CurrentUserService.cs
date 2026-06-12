using Microsoft.EntityFrameworkCore;
using CMES.Data;

namespace CMES.Services
{
    // Detected Windows user ki info (CMES_USERS se).
    public record CmesUserInfo(string Username, string? FullName, string Role);

    // Windows identity -> WWID -> CMES_USERS lookup. Scoped service.
    // AuthDbContext use karta hain (CMES_USERS alag AUTH_DB mein hain).
    public class CurrentUserService
    {
        private readonly IDbContextFactory<AuthDbContext> _factory;
        private readonly IHttpContextAccessor _http;
        private readonly IWebHostEnvironment _env;

        public CurrentUserService(IDbContextFactory<AuthDbContext> factory,
                                  IHttpContextAccessor http, IWebHostEnvironment env)
        {
            _factory = factory;
            _http = http;
            _env = env;
        }

        // "DOMAIN\\OD741" (ya "OD741") -> "OD741" (uppercase). Legacy FPA jaisa.
        public static string? ExtractWwid(string? identityName)
        {
            if (string.IsNullOrWhiteSpace(identityName)) return null;
            var slash = identityName.LastIndexOf('\\');
            var wwid = slash >= 0 ? identityName[(slash + 1)..] : identityName;
            wwid = wwid.Trim();
            return wwid.Length == 0 ? null : wwid.ToUpperInvariant();
        }

        // Current Windows WWID. Prod (IIS) HttpContext.User se aata hain;
        // Development mein agar identity na mile to Environment.UserName fallback (sirf dev).
        public string? CurrentWwid()
        {
            var name = _http.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(name) && _env.IsDevelopment())
                name = Environment.UserName;
            return ExtractWwid(name);
        }

        // Raw detected Windows identity ("DOMAIN\\wwid") - login landing pe dikhane ke liye.
        // DB check NAHI - sirf jo Windows ne diya. Dev mein DOMAIN\user fallback.
        public string CurrentIdentityName()
        {
            var name = _http.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(name) && _env.IsDevelopment())
            {
                var dom = Environment.UserDomainName;
                var usr = Environment.UserName;
                name = string.IsNullOrEmpty(dom) ? usr : $"{dom}\\{usr}";
            }
            return name ?? "";
        }

        // Active CMES user (ya null agar nahi mila / inactive).
        public async Task<CmesUserInfo?> GetActiveUserAsync()
        {
            var wwid = CurrentWwid();
            if (wwid == null) return null;

            await using var db = await _factory.CreateDbContextAsync();
            var u = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Username.ToUpper() == wwid && x.IsActive);
            return u == null ? null : new CmesUserInfo(u.Username.ToUpperInvariant(), u.FullName, u.Role);
        }
    }
}
