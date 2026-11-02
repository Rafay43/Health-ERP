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

    Task<List<HealthERP.Application.Admin.Models.UserAccountDto>> GetUserAccountsAsync(CancellationToken cancellationToken = default);

    Task<HealthERP.Application.Admin.Models.UserAccountDto> UpdateUserRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);

    Task<HealthERP.Application.Admin.Models.UserAccountDto> ToggleUserAccountStatusAsync(Guid userId, bool enableAccount, CancellationToken cancellationToken = default);

    Task<HealthERP.Application.Admin.Models.SystemHealthDto> GetSystemHealthAsync(CancellationToken cancellationToken = default);

    Task<(bool Success, string Message)> ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string Message, string[] Errors)> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
}
