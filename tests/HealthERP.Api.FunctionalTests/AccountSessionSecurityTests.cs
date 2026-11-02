using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HealthERP.Api.Authentication;
using HealthERP.Api.Endpoints;
using HealthERP.Api.Hubs;
using HealthERP.Api.Middleware;
using HealthERP.Application;
using HealthERP.Application.Authentication.Models;
using HealthERP.Application.Common.Configurations;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Security;
using HealthERP.Infrastructure.Identity;
using HealthERP.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace HealthERP.Api.FunctionalTests;

// Real Identity manager/store, password hashing/reset provider, endpoints and JWT pipeline.
// Only persistence is replaced with an isolated EF store; no ERP startup/migrations/email.
public class AccountSessionSecurityTests : IAsyncLifetime
{
    private const string UserEmail = "session-user@example.invalid";
    private const string AdminEmail = "session-admin@example.invalid";
    private const string Password = "Initial123!";
    private const string NewPassword = "Changed456!";
    private WebApplication _app = null!;
    private HttpClient _http = null!;
    private string _address = null!;
    private Guid _userId;
    private readonly ResetEmail _email = new();
    private readonly JwtSettings _settings = new()
    {
        Issuer = "AccountSessionTests", Audience = "AccountSessionTests",
        Secret = "isolated-account-tests-signing-key-at-least-32-characters", ExpiryMinutes = 5
    };

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddApplication();
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<TestIdentityDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
        }).AddRoles<ApplicationRole>().AddEntityFrameworkStores<TestIdentityDbContext>().AddDefaultTokenProviders();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton<IApplicationDbContext>(new Mock<IApplicationDbContext>().Object);
        builder.Services.AddSingleton<IEmailService>(_email);
        builder.Services.AddSingleton<IJwtTokenGenerator>(new JwtTokenGenerator(Options.Create(_settings)));
        builder.Services.AddScoped<IIdentityService, IdentityService>();
        builder.Services.AddScoped<IJwtSessionValidator, JwtSessionValidator>();
        builder.Services.AddAccountJwtAuthentication(_settings);
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.CanManageStaff, p => p.RequireRole(Roles.Admin));
            options.AddPolicy(Policies.CanPrescribeMedication, p => p.RequireRole(Roles.Admin, Roles.Doctor));
            options.AddPolicy(Policies.CanViewPatients, p => p.RequireRole(Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist));
        });
        builder.Services.AddSignalR();
        builder.Services.AddNotificationSecurity();
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        _app.UseAuthentication(); _app.UseAuthorization();
        _app.MapAuthEndpoints(); _app.MapAdminEndpoints(); _app.MapNotificationHubs();
        _app.MapGet("/test/protected", () => "ok").RequireAuthorization();
        _app.MapGet("/test/privileged", () => "ok").RequireAuthorization(Policies.CanPrescribeMedication);
        using (var scope = _app.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            foreach (var role in new[] { Roles.Admin, Roles.Doctor, Roles.BillingStaff, Roles.Patient })
                Assert.True((await roles.CreateAsync(new ApplicationRole { Name = role })).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var (email, role) in new[] { (UserEmail, Roles.Doctor), (AdminEmail, Roles.Admin) })
            {
                var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Test", LastName = "User" };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
                if (email == UserEmail) _userId = user.Id;
            }
        }
        await _app.StartAsync();
        _address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _http = new HttpClient { BaseAddress = new Uri(_address) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync(); await _app.DisposeAsync();
    }

    private async Task<HttpResponseMessage> Request(HttpMethod method, string path, string? token = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _http.SendAsync(request);
    }

    private async Task<AuthResponse> Login(string email = UserEmail, string password = Password)
    {
        using var result = await Request(HttpMethod.Post, "/api/v1/auth/login", body: new { email, password });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        return (await result.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task ExpectStatus(string token, HttpStatusCode expected, string path = "/test/protected")
    {
        using var response = await Request(HttpMethod.Get, path, token);
        Assert.Equal(expected, response.StatusCode);
    }

    private async Task EditUser(Func<UserManager<ApplicationUser>, ApplicationUser, Task> edit)
    {
        using var scope = _app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await manager.FindByIdAsync(_userId.ToString()))!;
        await edit(manager, user);
    }

    private async Task SetStatus(string adminToken, bool enable)
    {
        using var response = await Request(HttpMethod.Patch, $"/api/v1/admin/users/{_userId}/status", adminToken,
            new { enableAccount = enable });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task SetRole(string adminToken, string role)
    {
        using var response = await Request(HttpMethod.Put, $"/api/v1/admin/users/{_userId}/role", adminToken,
            new { roleName = role });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ActiveLogin_IssuesStampedJwt_AndProtectedRequestSucceeds()
    {
        var login = await Login();
        await ExpectStatus(login.Token, HttpStatusCode.OK);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.Token);
        var claim = Assert.Single(jwt.Claims, c => c.Type == JwtSessionVersion.ClaimName);
        Assert.Equal(64, claim.Value.Length);
        await EditUser((_, user) => {
            Assert.True(JwtSessionVersion.Matches(claim.Value, user.SecurityStamp));
            // Do not include either sensitive value in assertion failure output.
            Assert.True(claim.Value != user.SecurityStamp);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData("unknown-user")]
    [InlineData("inactive")]
    [InlineData("locked")]
    public async Task InvalidLogin_IsGeneric401_AndNeverIssuesJwt(string scenario)
    {
        if (scenario == "inactive") await EditUser(async (m, u) => { u.IsActive = false; Assert.True((await m.UpdateAsync(u)).Succeeded); });
        if (scenario == "locked") await EditUser(async (m, u) => {
            Assert.True((await m.SetLockoutEnabledAsync(u, true)).Succeeded);
            Assert.True((await m.SetLockoutEndDateAsync(u, DateTimeOffset.UtcNow.AddYears(100))).Succeeded);
        });
        using var response = await Request(HttpMethod.Post, "/api/v1/auth/login", body: new {
            email = scenario == "unknown-user" ? "unknown@example.invalid" : UserEmail,
            password = scenario == "wrong-password" ? "Wrong123!" : Password
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Invalid credentials.", body.RootElement.GetProperty("detail").GetString());
        Assert.False(body.RootElement.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task AdminLock_RejectsExistingJwtAndFreshLogin_UnlockDoesNotResurrectOldJwt()
    {
        var old = await Login(); var admin = await Login(AdminEmail);
        await SetStatus(admin.Token, false);
        await ExpectStatus(old.Token, HttpStatusCode.Unauthorized);
        using var denied = await Request(HttpMethod.Post, "/api/v1/auth/login", body: new { email = UserEmail, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await SetStatus(admin.Token, true);
        await ExpectStatus(old.Token, HttpStatusCode.Unauthorized);
        var fresh = await Login(); await ExpectStatus(fresh.Token, HttpStatusCode.OK);
    }

    [Fact]
    public async Task InactiveFlag_RejectsOldJwtIndependentOfLockout_AndAdminReactivationRequiresNewLogin()
    {
        var old = await Login(); var admin = await Login(AdminEmail);
        await EditUser(async (m, u) => {
            Assert.False(await m.IsLockedOutAsync(u));
            u.IsActive = false; Assert.True((await m.UpdateAsync(u)).Succeeded);
        });
        await ExpectStatus(old.Token, HttpStatusCode.Unauthorized);
        using var users = await Request(HttpMethod.Get, "/api/v1/admin/users", admin.Token);
        using var json = JsonDocument.Parse(await users.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.EnumerateArray().Single(u => u.GetProperty("id").GetGuid() == _userId).GetProperty("isActive").GetBoolean());
        await SetStatus(admin.Token, true);
        await ExpectStatus(old.Token, HttpStatusCode.Unauthorized);
        await ExpectStatus((await Login()).Token, HttpStatusCode.OK);
    }

    [Fact]
    public async Task PasswordReset_UsesValidIdentityResetToken_RevokesJwt_AndNewPasswordWorks()
    {
        var old = await Login();
        using var forgot = await Request(HttpMethod.Post, "/api/v1/auth/forgot-password", body: new { email = UserEmail });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        Assert.True(!string.IsNullOrEmpty(_email.Token));
        using var reset = await Request(HttpMethod.Post, "/api/v1/auth/reset-password", body: new {
            email = UserEmail, token = _email.Token, newPassword = NewPassword
        });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        await ExpectStatus(old.Token, HttpStatusCode.Unauthorized);
        using var oldPassword = await Request(HttpMethod.Post, "/api/v1/auth/login", body: new { email = UserEmail, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        await ExpectStatus((await Login(password: NewPassword)).Token, HttpStatusCode.OK);
        using var replay = await Request(HttpMethod.Post, "/api/v1/auth/reset-password", body: new {
            email = UserEmail, token = _email.Token, newPassword = "Another789!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task InvalidReset_DoesNotRotateStampOrInvalidateExistingJwt()
    {
        var old = await Login();
        using var reset = await Request(HttpMethod.Post, "/api/v1/auth/reset-password", body: new {
            email = UserEmail, token = "invalid-reset-token", newPassword = NewPassword
        });
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        await ExpectStatus(old.Token, HttpStatusCode.OK);
    }

    [Fact]
    public async Task RoleRemoval_RevokesPrivilegedJwt_NewTokenReflectsNewRoleAndReturns403()
    {
        var old = await Login(); var admin = await Login(AdminEmail);
        await ExpectStatus(old.Token, HttpStatusCode.OK, "/test/privileged");
        await SetRole(admin.Token, Roles.BillingStaff);
        await ExpectStatus(old.Token, HttpStatusCode.Unauthorized, "/test/privileged");
        var fresh = await Login();
        Assert.Contains(Roles.BillingStaff, fresh.Roles); Assert.DoesNotContain(Roles.Doctor, fresh.Roles);
        await ExpectStatus(fresh.Token, HttpStatusCode.OK);
        await ExpectStatus(fresh.Token, HttpStatusCode.Forbidden, "/test/privileged");
    }

    [Fact]
    public async Task RoleAddition_AlsoRequiresNewJwt_AndDoesNotChangePolicyDefinitions()
    {
        var admin = await Login(AdminEmail);
        await SetRole(admin.Token, Roles.BillingStaff);
        var old = await Login();
        await ExpectStatus(old.Token, HttpStatusCode.Forbidden, "/test/privileged");
        await SetRole(admin.Token, Roles.Doctor);
        await ExpectStatus(old.Token, HttpStatusCode.Unauthorized);
        var fresh = await Login(); Assert.Contains(Roles.Doctor, fresh.Roles);
        await ExpectStatus(fresh.Token, HttpStatusCode.OK, "/test/privileged");
    }

    [Fact]
    public async Task NonAdmin_CannotChangeAccountSecurityState()
    {
        var user = await Login();
        using var locked = await Request(HttpMethod.Patch, $"/api/v1/admin/users/{_userId}/status", user.Token, new { enableAccount = false });
        using var role = await Request(HttpMethod.Put, $"/api/v1/admin/users/{_userId}/role", user.Token, new { roleName = Roles.Admin });
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, role.StatusCode);
        await ExpectStatus(user.Token, HttpStatusCode.OK);
    }

    [Fact]
    public async Task IdentityFailedAttempts_LockAndRevoke_ExpiryDoesNotRestoreOldJwt()
    {
        var old = await Login();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var wrong = await Request(HttpMethod.Post, "/api/v1/auth/login", body: new { email = UserEmail, password = "Wrong123!" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }
        await EditUser(async (m, u) => {
            Assert.True(await m.IsLockedOutAsync(u));
            Assert.True((await m.SetLockoutEndDateAsync(u, DateTimeOffset.UtcNow.AddMinutes(-1))).Succeeded);
        });
        await ExpectStatus(old.Token, HttpStatusCode.Unauthorized);
        await ExpectStatus((await Login()).Token, HttpStatusCode.OK);
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsFailedAttemptCount_AndLockoutEnabledAloneDoesNotDenyLogin()
    {
        await EditUser(async (m, u) => {
            Assert.True((await m.SetLockoutEnabledAsync(u, true)).Succeeded);
            Assert.True((await m.AccessFailedAsync(u)).Succeeded);
        });
        await ExpectStatus((await Login()).Token, HttpStatusCode.OK);
        await EditUser(async (m, u) => Assert.Equal(0, await m.GetAccessFailedCountAsync(u)));
    }

    [Fact]
    public async Task DeletedUser_CannotContinueWithOldJwt()
    {
        var old = await Login();
        await EditUser(async (m, u) => Assert.True((await m.DeleteAsync(u)).Succeeded));
        await ExpectStatus(old.Token, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("stale")]
    public async Task LegacyOrInvalidSessionClaim_IsRejectedEvenWithValidSignature(string scenario)
    {
        var login = await Login();
        var original = new JwtSecurityTokenHandler().ReadJwtToken(login.Token);
        var claims = original.Claims.Where(c => c.Type != JwtSessionVersion.ClaimName).ToList();
        if (scenario != "missing") claims.Add(new Claim(JwtSessionVersion.ClaimName,
            scenario == "malformed" ? new string('Z', 64) : JwtSessionVersion.FromSecurityStamp("unrelated-stamp")));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(_settings.Issuer, _settings.Audience,
            claims, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret)), SecurityAlgorithms.HmacSha256)));
        await ExpectStatus(token, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HubJwtValidation_ValidConnectionWorks_RevokedNegotiationAndReconnectFail()
    {
        var old = await Login(); var admin = await Login(AdminEmail);
        using var valid = await Request(HttpMethod.Post, "/hubs/notifications/negotiate?negotiateVersion=1", old.Token);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(_address.Replace("http://", "ws://") + "/hubs/notifications?access_token=" + old.Token), CancellationToken.None);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[512];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var handshake = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
        Assert.Contains("{}", Encoding.UTF8.GetString(buffer, 0, handshake.Count));
        await SetStatus(admin.Token, false);
        foreach (var hub in new[] { "/hubs/notifications", "/hubs/appointments" })
        {
            using var denied = await Request(HttpMethod.Post, hub + "/negotiate?negotiateVersion=1&access_token=" + old.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using var reconnect = new ClientWebSocket();
        await Assert.ThrowsAsync<WebSocketException>(() => reconnect.ConnectAsync(
            new Uri(_address.Replace("http://", "ws://") + "/hubs/notifications?access_token=" + old.Token), CancellationToken.None));
        socket.Abort();
        await SetStatus(admin.Token, true);
        using var fresh = await Request(HttpMethod.Post, "/hubs/notifications/negotiate?negotiateVersion=1", (await Login()).Token);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    public sealed class TestIdentityDbContext(DbContextOptions<TestIdentityDbContext> options)
        : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options);

    private sealed class ResetEmail : IEmailService
    {
        public string? Token { get; private set; }
        public Task SendPasswordResetEmailAsync(string email, string firstName, string token, CancellationToken cancellationToken = default)
        { Token = token; return Task.CompletedTask; }
        public Task SendPatientActivationCodeEmailAsync(string email, string firstName, string code, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
