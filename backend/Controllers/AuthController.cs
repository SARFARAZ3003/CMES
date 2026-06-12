using Microsoft.AspNetCore.Mvc;
using CMES.Services;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly CurrentUserService _users;
        public AuthController(CurrentUserService users) => _users = users;

        // GET /api/auth/whoami
        // Sirf detected Windows identity (DB check ke bina) - login landing page pe dikhane ke liye.
        [HttpGet("whoami")]
        public IActionResult WhoAmI() =>
            Ok(new { display = _users.CurrentIdentityName(), wwid = _users.CurrentWwid() });

        // GET /api/auth/me
        // Frontend sabse pehle ye call karta hain. Active CMES user -> 200 + details,
        // warna 403 (Access Denied screen ke liye).
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var user = await _users.GetActiveUserAsync();
            if (user != null)
                return Ok(new
                {
                    authorized = true,
                    username = user.Username,
                    fullName = user.FullName,
                    role = user.Role
                });

            // Detected but not authorized (na mila ya inactive).
            return StatusCode(403, new
            {
                authorized = false,
                username = _users.CurrentWwid(),
                message = "Access denied. Your Windows account is not registered or is inactive in CMES."
            });
        }
    }
}
