using HealthERP.Api.Authentication;
using HealthERP.Application.Common.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace HealthERP.Api.FunctionalTests;

public class AuthenticationConfigurationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("short-test-key")]
    [InlineData("<generate-a-random-signing-key-and-store-in-user-secrets>")]
    public void MissingWeakOrExampleSigningKeysFailWithoutEchoingTheValue(string secret)
    {
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() => services.AddAccountJwtAuthentication(new JwtSettings
        {
            Secret = secret, Issuer = "ConfigurationTests", Audience = "ConfigurationTests"
        }));
        Assert.Contains("Configure JwtSettings:Secret", error.Message);
        if (secret.Length > 0) Assert.DoesNotContain(secret, error.Message);
    }
}
