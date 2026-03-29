using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Common.Identity
{
    /// <summary>
    /// Lấy User từ JWT: token mới (NameIdentifier = user Id) hoặc token cũ (NameIdentifier = email).
    /// </summary>
    public static class CurrentUserResolver
    {
        public static async Task<User?> GetCurrentUserAsync(this UserManager<User> userManager, ClaimsPrincipal principal)
        {
            var user = await userManager.GetUserAsync(principal);
            if (user != null)
                return user;

            var nameId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(nameId))
                return null;

            if (nameId.Contains('@', StringComparison.Ordinal))
                return await userManager.FindByEmailAsync(nameId);

            return await userManager.FindByIdAsync(nameId);
        }
    }
}
