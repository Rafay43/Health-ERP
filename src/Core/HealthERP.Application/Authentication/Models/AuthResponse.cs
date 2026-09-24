namespace HealthERP.Application.Authentication.Models;

public record AuthResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Token,
    IEnumerable<string> Roles
);
