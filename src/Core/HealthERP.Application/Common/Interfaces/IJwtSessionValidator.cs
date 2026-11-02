namespace HealthERP.Application.Common.Interfaces;

public interface IJwtSessionValidator
{
    Task<bool> ValidateAsync(Guid userId, string? sessionVersion, CancellationToken cancellationToken = default);
}
