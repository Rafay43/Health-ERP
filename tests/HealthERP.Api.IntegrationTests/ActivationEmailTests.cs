using HealthERP.Application.Common.Configurations;
using HealthERP.Application.PatientAccount;
using HealthERP.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace HealthERP.Api.IntegrationTests;

public class ActivationEmailTests
{
    private sealed class Capture : ILogger<EmailService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState,Exception?,string> formatter)
            => Messages.Add(formatter(state,exception));
    }
    [Theory] [InlineData("Production")] [InlineData("Staging")]
    public async Task NonDevelopmentCannotSimulateDeliveryOrLogRawOtp(string name)
    {
        var environment = new Mock<IHostEnvironment>(); environment.SetupGet(e => e.EnvironmentName).Returns(name);
        var log = new Capture(); var service = new EmailService(Options.Create(new EmailSettings { Host="localhost" }),log,environment.Object);
        await Assert.ThrowsAsync<ActivationEmailDeliveryException>(() => service.SendPatientActivationCodeEmailAsync("test@example.invalid","Test","123456"));
        Assert.DoesNotContain(log.Messages,m => m.Contains("123456"));
    }
    [Fact] public async Task ExplicitDevelopmentAllowsSimulation()
    {
        var environment = new Mock<IHostEnvironment>(); environment.SetupGet(e => e.EnvironmentName).Returns("Development");
        var log = new Capture(); var service = new EmailService(Options.Create(new EmailSettings { Host="localhost" }),log,environment.Object);
        await service.SendPatientActivationCodeEmailAsync("test@example.invalid","Test","123456");
        Assert.Contains(log.Messages,m => m.Contains("123456"));
    }
}
