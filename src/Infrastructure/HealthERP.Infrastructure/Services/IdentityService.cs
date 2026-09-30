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

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
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

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateToken(user.Id.ToString(), user.Email!, user.FirstName, user.LastName, roles);

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
        if (user is null)
        {
            return (false, null, new[] { "Invalid credentials." });
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            return (false, null, new[] { "Invalid credentials." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateToken(user.Id.ToString(), user.Email!, user.FirstName, user.LastName, roles);

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
            var isActive = !user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow;
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
            await _roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        await _userManager.AddToRoleAsync(user, roleName);

        var updatedRoles = await _userManager.GetRolesAsync(user);
        var isActive = !user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow;

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

        if (enableAccount)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
        }
        else
        {
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserAccountDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            roles.ToList(),
            enableAccount,
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
}
