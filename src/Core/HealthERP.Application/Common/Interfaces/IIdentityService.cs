using HealthERP.Application.Authentication.Models;

namespace HealthERP.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<(bool Success, AuthResponse? Response, string[] Errors)> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);

    Task<(bool Success, AuthResponse? Response, string[] Errors)> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}
