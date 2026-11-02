using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Authentication.Models;
using HealthERP.Application.Admin.Models;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Infrastructure.Identity;

namespace HealthERP.Infrastructure.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IApplicationDbContext context,
        IEmailService emailService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
        _emailService = emailService;
    }

    public async Task<(bool Success, AuthResponse? Response, string[] Errors)> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return (false, null, new[] { "User with this email already exists." });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToArray();
            return (false, null, errors);
        }

        // Server-side assignment of Patient role for all public registrations
        var roleResult = await _userManager.AddToRoleAsync(user, HealthERP.Application.Common.Security.Roles.Patient);
        if (!roleResult.Succeeded)
        {
            // Transactional rollback: delete created user if role assignment fails
            await _userManager.DeleteAsync(user);
            var errors = roleResult.Errors.Select(e => e.Description).ToArray();
            return (false, null, errors);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateToken(user.Id.ToString(), user.Email!, user.FirstName, user.LastName, roles,
            await _userManager.GetSecurityStampAsync(user));

        var response = new AuthResponse(
            user.Id.ToString(),
            user.Email!,
            user.FirstName,
            user.LastName,
            token,
            roles
        );

        return (true, response, Array.Empty<string>());
    }

    public async Task<(bool Success, AuthResponse? Response, string[] Errors)> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive || await _userManager.IsLockedOutAsync(user))
        {
            return (false, null, new[] { "Invalid credentials." });
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            if (_userManager.SupportsUserLockout && await _userManager.GetLockoutEnabledAsync(user))
            {
                EnsureSucceeded(await _userManager.AccessFailedAsync(user));
                if (await _userManager.IsLockedOutAsync(user))
                    EnsureSucceeded(await _userManager.UpdateSecurityStampAsync(user));
            }
            return (false, null, new[] { "Invalid credentials." });
        }

        if (_userManager.SupportsUserLockout)
            EnsureSucceeded(await _userManager.ResetAccessFailedCountAsync(user));

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateToken(user.Id.ToString(), user.Email!, user.FirstName, user.LastName, roles,
            await _userManager.GetSecurityStampAsync(user));

        var response = new AuthResponse(
            user.Id.ToString(),
            user.Email!,
            user.FirstName,
            user.LastName,
            token,
            roles
        );

        return (true, response, Array.Empty<string>());
    }

    public async Task<List<UserAccountDto>> GetUserAccountsAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<UserAccountDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var isActive = user.IsActive && !await _userManager.IsLockedOutAsync(user);
            result.Add(new UserAccountDto(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                user.FirstName,
                user.LastName,
                roles.ToList(),
                isActive,
                user.CreatedAtUtc
            ));
        }

        return result;
    }

    public async Task<UserAccountDto> UpdateUserRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            throw new KeyNotFoundException($"User with ID '{userId}' was not found.");

        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            EnsureSucceeded(await _roleManager.CreateAsync(new ApplicationRole { Name = roleName }));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        // Revoke before changing roles, so even a partially failed update cannot retain old privileges.
        EnsureSucceeded(await _userManager.UpdateSecurityStampAsync(user));
        try
        {
            if (currentRoles.Any())
                EnsureSucceeded(await _userManager.RemoveFromRolesAsync(user, currentRoles));
            EnsureSucceeded(await _userManager.AddToRoleAsync(user, roleName));
        }
        finally
        {
            // Also revoke tokens issued while role replacement was in progress.
            EnsureSucceeded(await _userManager.UpdateSecurityStampAsync(user));
        }

        var updatedRoles = await _userManager.GetRolesAsync(user);
        var isActive = user.IsActive && !await _userManager.IsLockedOutAsync(user);

        return new UserAccountDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            updatedRoles.ToList(),
            isActive,
            user.CreatedAtUtc
        );
    }

    public async Task<UserAccountDto> ToggleUserAccountStatusAsync(Guid userId, bool enableAccount, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            throw new KeyNotFoundException($"User with ID '{userId}' was not found.");

        // Rotate for both directions: unlocking/reactivating must never revive a revoked JWT.
        EnsureSucceeded(await _userManager.UpdateSecurityStampAsync(user));
        user.IsActive = enableAccount;
        EnsureSucceeded(await _userManager.UpdateAsync(user));
        if (enableAccount)
        {
            EnsureSucceeded(await _userManager.SetLockoutEndDateAsync(user, null));
            EnsureSucceeded(await _userManager.ResetAccessFailedCountAsync(user));
        }
        else
        {
            EnsureSucceeded(await _userManager.SetLockoutEnabledAsync(user, true));
            EnsureSucceeded(await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100)));
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserAccountDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            roles.ToList(),
            user.IsActive && !await _userManager.IsLockedOutAsync(user),
            user.CreatedAtUtc
        );
    }

    public async Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken cancellationToken = default)
    {
        bool canConnect = true;
        try
        {
            canConnect = await _context.Patients.AnyAsync(cancellationToken) || true;
        }
        catch
        {
            canConnect = false;
        }

        var totalUsers = await _userManager.Users.CountAsync(cancellationToken);
        var totalRoles = await _roleManager.Roles.CountAsync(cancellationToken);

        return new SystemHealthDto(
            canConnect ? "Healthy" : "Degraded",
            "Npgsql (PostgreSQL 16)",
            canConnect,
            totalUsers,
            totalRoles,
            DateTime.UtcNow
        );
    }

    public async Task<(bool Success, string Message)> ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var genericResponseMsg = "If an account exists for this email address, we've sent password reset instructions.";

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Always return generic success to prevent email/account enumeration
            return (true, genericResponseMsg);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        await _emailService.SendPasswordResetEmailAsync(user.Email!, user.FirstName, token, cancellationToken);

        return (true, genericResponseMsg);
    }

    public async Task<(bool Success, string Message, string[] Errors)> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return (false, "Failed to reset password.", new[] { "Invalid reset request or token expired." });
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToArray();
            return (false, "Failed to reset password.", errors);
        }

        // Identity's successful ResetPasswordAsync already rotates SecurityStamp.
        // Do not rotate it before reset-token validation or perform a redundant update.

        return (true, "Password has been successfully reset. You can now log in with your new password.", Array.Empty<string>());
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("The account security update could not be completed.");
    }
}
