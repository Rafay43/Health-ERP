using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Security;
using HealthERP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace HealthERP.Infrastructure.Services;

public sealed class JwtSessionValidator(UserManager<ApplicationUser> userManager) : IJwtSessionValidator
{
    public async Task<bool> ValidateAsync(Guid userId, string? sessionVersion, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || sessionVersion?.Length != 64) return false;
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is not null && user.IsActive && !await userManager.IsLockedOutAsync(user)
            && JwtSessionVersion.Matches(sessionVersion, user.SecurityStamp);
    }
}
