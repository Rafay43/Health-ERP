using HealthERP.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HealthERP.Api.IntegrationTests;

public class DevelopmentSeedingTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task NonDevelopmentCannotSeedEvenWhenExplicitlyEnabled(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentSeed:Enabled"] = "true"
        }).Build();
        // Null stores prove rejection happens before any database/account access.
        var initialiser = new ApplicationDbContextInitialiser(NullLogger<ApplicationDbContextInitialiser>.Instance,
            null!, null!, null!, configuration, environment.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => initialiser.TrySeedAsync());
    }
}
