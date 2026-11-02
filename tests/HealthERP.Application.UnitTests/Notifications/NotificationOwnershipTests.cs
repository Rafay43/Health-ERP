using System.Text.Json;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Notifications.Commands;
using HealthERP.Application.Notifications.Models;
using HealthERP.Application.Notifications.Queries;
using HealthERP.Domain.Notifications;
using MockQueryable.Moq;
using Moq;

namespace HealthERP.Application.UnitTests.Notifications;

public class NotificationOwnershipTests
{
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<ICurrentUserService> _user = new();
    private readonly List<Notification> _rows;

    public NotificationOwnershipTests()
    {
        _rows = [Create(_userA), Create(_userB), Create(null)];
        var set = _rows.BuildMockDbSet();
        set.Setup(s => s.Add(It.IsAny<Notification>())).Callback<Notification>(_rows.Add);
        _context.Setup(c => c.Notifications).Returns(set.Object);
    }

    private static Notification Create(Guid? owner) => Notification.Create(
        "Test notification", "Private message", NotificationType.SystemInfo, NotificationSeverity.Info,
        targetUserId: owner, targetRole: "All");

    [Fact]
    public async Task Retrieval_IsScopedForBothUsers_DespiteLegacyAllRole()
    {
        var handler = new GetUserNotificationsQueryHandler(_context.Object, _user.Object);
        foreach (var owner in new[] { _userA, _userB })
        {
            _user.SetupGet(u => u.UserId).Returns(owner.ToString());
            var result = await handler.Handle(new(), default);
            Assert.Equal(_rows.Single(n => n.TargetUserId == owner).Id.Value, Assert.Single(result).Id);
        }
    }

    [Fact]
    public async Task ReadAnotherUsersNotification_IsIndistinguishableFromMissing_AndDoesNotSave()
    {
        _user.SetupGet(u => u.UserId).Returns(_userB.ToString());
        var handler = new MarkNotificationAsReadCommandHandler(_context.Object, _user.Object);
        Assert.False(await handler.Handle(new(_rows[0].Id.Value), default));
        Assert.False(await handler.Handle(new(Guid.NewGuid()), default));
        Assert.False(_rows[0].IsRead);
        Assert.Null(_rows[0].ReadAtUtc);
        _context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OwnerCanRead_AndUnreadQueryExcludesReadRecord()
    {
        _user.SetupGet(u => u.UserId).Returns(_userA.ToString());
        Assert.True(await new MarkNotificationAsReadCommandHandler(_context.Object, _user.Object)
            .Handle(new(_rows[0].Id.Value), default));
        Assert.True(_rows[0].IsRead);
        Assert.NotNull(_rows[0].ReadAtUtc);
        Assert.Empty(await new GetUserNotificationsQueryHandler(_context.Object, _user.Object).Handle(new(true), default));
        Assert.Single(await new GetUserNotificationsQueryHandler(_context.Object, _user.Object).Handle(new(), default));
    }

    [Fact]
    public async Task ReadAll_ChangesOnlyOwnersRows()
    {
        _user.SetupGet(u => u.UserId).Returns(_userB.ToString());
        Assert.True(await new MarkAllNotificationsAsReadCommandHandler(_context.Object, _user.Object).Handle(new(), default));
        Assert.False(_rows[0].IsRead);
        Assert.True(_rows[1].IsRead);
        Assert.False(_rows[2].IsRead);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-identity-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task MissingOrInvalidOwner_FailsClosed(string? userId)
    {
        _user.SetupGet(u => u.UserId).Returns(userId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetUserNotificationsQueryHandler(_context.Object, _user.Object).Handle(new(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new MarkNotificationAsReadCommandHandler(_context.Object, _user.Object).Handle(new(_rows[0].Id.Value), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new MarkAllNotificationsAsReadCommandHandler(_context.Object, _user.Object).Handle(new(), default));
        Assert.All(_rows, n => Assert.False(n.IsRead));
    }

    [Fact]
    public async Task Create_IsOwnedByAuthenticatedActor_AndPublishesOnlyAfterSave()
    {
        _user.SetupGet(u => u.UserId).Returns(_userA.ToString());
        var saved = false;
        _context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => saved = true).ReturnsAsync(1);
        var publisher = new Mock<IPrivateNotificationPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => {
                Assert.True(saved);
                Assert.Equal(_userA, n.TargetUserId);
                Assert.Contains(n, _rows);
            }).Returns(Task.CompletedTask);
        var dto = await new SendNotificationCommandHandler(_context.Object, _user.Object, publisher.Object)
            .Handle(new("Owner notification", "Private", NotificationType.SystemInfo, NotificationSeverity.Info), default);
        Assert.Equal(_userA, _rows.Single(n => n.Id.Value == dto.Id).TargetUserId);
        publisher.Verify(p => p.PublishAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Payload_OmitsOwnershipAndClinicalEntityFields()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(NotificationDto.FromEntity(_rows[0]), JsonSerializerOptions.Web));
        var fields = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("targetUserId", fields);
        Assert.DoesNotContain("targetRole", fields);
        Assert.DoesNotContain("patientId", fields);
        Assert.DoesNotContain("createdBy", fields);
        Assert.Contains("id", fields); // Required by the read action.
    }
}
