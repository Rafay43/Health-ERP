using Microsoft.AspNetCore.Identity;
using HealthERP.Application.Authentication.Models;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Infrastructure.Identity;

namespace HealthERP.Infrastructure.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
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
}
