using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HealthERP.Api.Endpoints;
using HealthERP.Api.Authentication;
using HealthERP.Api.Hubs;
using HealthERP.Api.Services;
using HealthERP.Application;
using HealthERP.Application.Common.Configurations;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Security;
using HealthERP.Application.Notifications.Models;
using HealthERP.Domain.Notifications;
using HealthERP.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockQueryable.Moq;
using Moq;

namespace HealthERP.Api.FunctionalTests;

// Uses the actual endpoints, handlers, JWT generator, hub mappings and publisher.
// The isolated host substitutes only persistence; it never runs Program's database initializer.
public class NotificationSecurityTests : IAsyncLifetime
{
    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();
    private readonly List<Notification> _rows = [];
    private WebApplication _app = null!;
    private HttpClient _http = null!;
    private string _address = null!;
    private readonly JwtSettings _settings = new()
    {
        Issuer = "NotificationTests", Audience = "NotificationTests",
        Secret = "isolated-test-signing-key-with-at-least-32-characters", ExpiryMinutes = 5
    };

    public async Task InitializeAsync()
    {
        _rows.Add(Create(_a)); _rows.Add(Create(_b)); _rows.Add(Create(null));
        var context = new Mock<IApplicationDbContext>();
        var set = _rows.BuildMockDbSet();
        set.Setup(s => s.Add(It.IsAny<Notification>())).Callback<Notification>(_rows.Add);
        context.Setup(c => c.Notifications).Returns(set.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders(); // Query transport tokens must not appear in test output.
        builder.Services.AddApplication();
        builder.Services.AddSingleton(context.Object);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddScoped<IPrivateNotificationPublisher, NotificationPublisher>();
        var sessions = new Mock<IJwtSessionValidator>();
        sessions.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string? version, CancellationToken _) =>
                (id == _a || id == _b) && JwtSessionVersion.Matches(version, "notification-test-stamp"));
        builder.Services.AddSingleton(sessions.Object);
        builder.Services.AddAccountJwtAuthentication(_settings);
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.CanEditPatients, p => p.RequireRole(Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist));
            options.AddPolicy(Policies.CanViewPatients, p => p.RequireRole(Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist));
        });
        builder.Services.AddSignalR();
        builder.Services.AddNotificationSecurity();
        _app = builder.Build();
        _app.UseAuthentication(); _app.UseAuthorization();
        _app.MapNotificationEndpoints(); _app.MapNotificationHubs();
        await _app.StartAsync();
        _address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _http = new HttpClient { BaseAddress = new Uri(_address) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static Notification Create(Guid? owner) => Notification.Create("Private", "Owned message",
        NotificationType.SystemInfo, NotificationSeverity.Info, targetUserId: owner);

    private string Token(Guid user, string role = Roles.Doctor) =>
        new JwtTokenGenerator(Options.Create(_settings)).GenerateToken(user.ToString().ToUpperInvariant(),
            "test@example.invalid", "Test", "User", [role], "notification-test-stamp");

    private async Task<HttpResponseMessage> Request(HttpMethod method, string path, string? token = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) request.Content = JsonContent.Create(body);
        return await _http.SendAsync(request);
    }

    [Theory]
    [InlineData("GET", "/api/v1/notifications/")]
    [InlineData("POST", "/api/v1/notifications/")]
    [InlineData("POST", "/api/v1/notifications/00000000-0000-0000-0000-000000000001/read")]
    [InlineData("POST", "/api/v1/notifications/read-all")]
    [InlineData("POST", "/hubs/notifications/negotiate?negotiateVersion=1")]
    [InlineData("POST", "/hubs/appointments/negotiate?negotiateVersion=1")]
    public async Task UnauthenticatedRequests_AreRejected(string method, string path)
    {
        using var response = await Request(new HttpMethod(method), path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HttpOwnership_ReadAndReadAllAreIsolated_AndClientUserIdCannotOverride()
    {
        foreach (var owner in new[] { _a, _b })
        {
            using var list = await Request(HttpMethod.Get, $"/api/v1/notifications/?userId={_a}&role=All", Token(owner));
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Equal(_rows.Single(n => n.TargetUserId == owner).Id.Value,
                Assert.Single((await list.Content.ReadFromJsonAsync<List<NotificationDto>>())!).Id);
        }
        using var denied = await Request(HttpMethod.Post, $"/api/v1/notifications/{_rows[0].Id.Value}/read", Token(_b));
        using var missing = await Request(HttpMethod.Post, $"/api/v1/notifications/{Guid.NewGuid()}/read", Token(_b));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(missing.StatusCode, denied.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await denied.Content.ReadAsStringAsync());
        Assert.False(_rows[0].IsRead);
        using var read = await Request(HttpMethod.Post, $"/api/v1/notifications/{_rows[0].Id.Value}/read", Token(_a));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.True(_rows[0].IsRead);
        using var all = await Request(HttpMethod.Post, "/api/v1/notifications/read-all", Token(_b));
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.True(_rows[1].IsRead);
        Assert.False(_rows[2].IsRead);
    }

    [Fact]
    public async Task QueryToken_IsAcceptedOnlyOnHubRoutes_AndPatientCannotCreateStaffNotifications()
    {
        using var hub = await Request(HttpMethod.Post, $"/hubs/notifications/negotiate?negotiateVersion=1&access_token={Token(_a)}");
        Assert.Equal(HttpStatusCode.OK, hub.StatusCode);
        using var api = await Request(HttpMethod.Get, $"/api/v1/notifications/?access_token={Token(_a)}");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        using var patient = await Request(HttpMethod.Post, "/api/v1/notifications/", Token(_b, Roles.Patient),
            new { title = "Test", message = "Test", type = 5, severity = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, patient.StatusCode);
        using var appointment = await Request(HttpMethod.Post, "/hubs/appointments/negotiate?negotiateVersion=1", Token(_b, Roles.Patient));
        Assert.Equal(HttpStatusCode.Forbidden, appointment.StatusCode);
        using var patientList = await Request(HttpMethod.Get, "/api/v1/notifications/", Token(_b, Roles.Patient));
        Assert.Equal(_rows[1].Id.Value, Assert.Single((await patientList.Content.ReadFromJsonAsync<List<NotificationDto>>())!).Id);
        using var invalidOwner = await Request(HttpMethod.Post, "/hubs/notifications/negotiate?negotiateVersion=1", Token(Guid.Empty));
        Assert.Equal(HttpStatusCode.Unauthorized, invalidOwner.StatusCode);
    }

    [Fact]
    public async Task RealWebSockets_DeliverToBothOwnerConnections_AndNeverToOtherUser()
    {
        using var a1 = await Connect(_a);
        using var a2 = await Connect(_a);
        using var b = await Connect(_b);
        using var created = await Request(HttpMethod.Post, "/api/v1/notifications/", Token(_a),
            new { title = "For A", message = "Only A", type = 5, severity = 0, targetUserId = _b, targetRole = "All" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = (await created.Content.ReadFromJsonAsync<NotificationDto>())!;
        Assert.Equal(_a, _rows.Single(n => n.Id.Value == dto.Id).TargetUserId);
        Assert.Equal(dto.Id.ToString(), (await a1.Invocation()).GetProperty("arguments")[0].GetProperty("id").GetString());
        Assert.Equal(dto.Id.ToString(), (await a2.Invocation()).GetProperty("arguments")[0].GetProperty("id").GetString());
        // B's own event is a barrier: if A leaked, B's first queued invocation would be A's event.
        using var bCreated = await Request(HttpMethod.Post, "/api/v1/notifications/", Token(_b),
            new { title = "For B", message = "Only B", type = 5, severity = 0 });
        var bDto = (await bCreated.Content.ReadFromJsonAsync<NotificationDto>())!;
        var bEvent = await b.Invocation();
        Assert.Equal("ReceiveNotification", bEvent.GetProperty("target").GetString());
        Assert.Equal(bDto.Id.ToString(), bEvent.GetProperty("arguments")[0].GetProperty("id").GetString());
        Assert.False(bEvent.GetProperty("arguments")[0].TryGetProperty("targetUserId", out _));
        // A's next own event also proves B's event was not queued for either A connection.
        using var aAgain = await Request(HttpMethod.Post, "/api/v1/notifications/", Token(_a),
            new { title = "A again", message = "Only A again", type = 5, severity = 0 });
        var aAgainDto = (await aAgain.Content.ReadFromJsonAsync<NotificationDto>())!;
        Assert.Equal(aAgainDto.Id.ToString(), (await a1.Invocation()).GetProperty("arguments")[0].GetProperty("id").GetString());
        Assert.Equal(aAgainDto.Id.ToString(), (await a2.Invocation()).GetProperty("arguments")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task UnauthenticatedWebSocketUpgrade_IsRejected()
    {
        using var socket = new ClientWebSocket();
        await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(
            new Uri(_address.Replace("http://", "ws://") + NotificationHub.HubUrl), CancellationToken.None));
    }

    private async Task<HubSocket> Connect(Guid user)
    {
        var socket = new HubSocket();
        await socket.Socket.ConnectAsync(new Uri(_address.Replace("http://", "ws://") +
            NotificationHub.HubUrl + "?access_token=" + Token(user)), CancellationToken.None);
        await socket.Socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"),
            WebSocketMessageType.Text, true, CancellationToken.None);
        var handshake = await socket.Frame();
        Assert.Equal("{}", handshake);
        return socket;
    }

    private sealed class HubSocket : IDisposable
    {
        public ClientWebSocket Socket { get; } = new();
        private string _buffer = "";
        public async Task<string> Frame()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!_buffer.Contains('\u001e'))
            {
                var bytes = new byte[8192];
                var result = await Socket.ReceiveAsync(bytes.AsMemory(), timeout.Token);
                if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("Hub closed unexpectedly.");
                _buffer += Encoding.UTF8.GetString(bytes, 0, result.Count);
            }
            var index = _buffer.IndexOf('\u001e');
            var frame = _buffer[..index];
            _buffer = _buffer[(index + 1)..];
            return frame;
        }
        public async Task<JsonElement> Invocation()
        {
            while (true)
            {
                using var json = JsonDocument.Parse(await Frame());
                if (json.RootElement.GetProperty("type").GetInt32() == 1) return json.RootElement.Clone();
            }
        }
        public void Dispose() { Socket.Abort(); Socket.Dispose(); }
    }
}
