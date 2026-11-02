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
            => Messages.Add(formatter(state,exception) + exception?.ToString());
    }
    [Theory] [InlineData("Production")] [InlineData("Staging")]
    public async Task NonDevelopmentCannotSimulateDeliveryOrLogRawOtp(string name)
    {
        var environment = new Mock<IHostEnvironment>(); environment.SetupGet(e => e.EnvironmentName).Returns(name);
        var log = new Capture(); var service = new EmailService(Options.Create(new EmailSettings { Host="localhost" }),log,environment.Object);
        await Assert.ThrowsAsync<ActivationEmailDeliveryException>(() => service.SendPatientActivationCodeEmailAsync("test@example.invalid","Test","123456"));
        Assert.DoesNotContain(log.Messages,m => m.Contains("123456"));
    }
    [Fact] public async Task ExplicitDevelopmentAllowsSimulationWithoutLoggingTheCode()
    {
        var environment = new Mock<IHostEnvironment>(); environment.SetupGet(e => e.EnvironmentName).Returns("Development");
        var log = new Capture(); var service = new EmailService(Options.Create(new EmailSettings { Host="localhost" }),log,environment.Object);
        await service.SendPatientActivationCodeEmailAsync("test@example.invalid","Test","123456");
        Assert.DoesNotContain(log.Messages,m => m.Contains("123456"));
        Assert.Contains(log.Messages,m => m.Contains("simulation completed"));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task PasswordResetWithoutSmtpNeverLogsTokenOrRecipient(string name)
    {
        var environment = new Mock<IHostEnvironment>(); environment.SetupGet(e => e.EnvironmentName).Returns(name);
        var log = new Capture();
        var service = new EmailService(Options.Create(new EmailSettings { Host = "localhost" }), log, environment.Object);
        await service.SendPasswordResetEmailAsync("private@example.invalid", "Test", "sensitive-reset-token+/=");
        Assert.NotEmpty(log.Messages);
        Assert.DoesNotContain(log.Messages, m => m.Contains("sensitive-reset") || m.Contains("private@example.invalid") || m.Contains("?token="));
    }

    [Fact]
    public async Task PasswordResetDeliveryFailureDoesNotLogCredentialsOrToken()
    {
        var environment = new Mock<IHostEnvironment>();
        var log = new Capture();
        var service = new EmailService(Options.Create(new EmailSettings
        {
            Host = "127.0.0.1", Port = -1, Username = "smtp-user", Password = "isolated-smtp-test-password"
        }), log, environment.Object);
        await service.SendPasswordResetEmailAsync("private@example.invalid", "Test", "sensitive-reset-token");
        Assert.Contains(log.Messages, m => m.Contains("delivery failed"));
        Assert.DoesNotContain(log.Messages, m => m.Contains("sensitive-reset") || m.Contains("isolated-smtp") || m.Contains("private@example.invalid"));
    }

    [Fact]
    public async Task ActivationDeliveryFailureDoesNotLogOtpOrCredentials()
    {
        var environment = new Mock<IHostEnvironment>();
        var log = new Capture();
        var service = new EmailService(Options.Create(new EmailSettings
        {
            Host = "127.0.0.1", Port = -1, Password = "isolated-smtp-test-password"
        }), log, environment.Object);
        await Assert.ThrowsAsync<ActivationEmailDeliveryException>(() => service.SendPatientActivationCodeEmailAsync("private@example.invalid", "Private Patient", "987654"));
        Assert.Contains(log.Messages, m => m.Contains("delivery failed"));
        Assert.DoesNotContain(log.Messages, m => m.Contains("987654") || m.Contains("isolated-smtp") || m.Contains("private@example.invalid") || m.Contains("Private Patient"));
    }
}
