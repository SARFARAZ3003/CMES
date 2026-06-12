using Microsoft.AspNetCore.Authorization;
using CMES.Services;

namespace CMES.Authorization
{
    // Policy "CmesUser": current Windows user CMES_USERS mein active hona chahiye.
    public class CmesUserRequirement : IAuthorizationRequirement { }

    public class CmesUserHandler : AuthorizationHandler<CmesUserRequirement>
    {
        private readonly CurrentUserService _users;
        public CmesUserHandler(CurrentUserService users) => _users = users;

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, CmesUserRequirement requirement)
        {
            // Active user mila to allow, warna fail (403 Forbidden).
            var user = await _users.GetActiveUserAsync();
            if (user != null)
                context.Succeed(requirement);
        }
    }
}
