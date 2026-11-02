using HealthERP.Application.Appointments.Commands.BookAppointment;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.StaffMembers.Commands.CreateStaff;
using HealthERP.Application.Triage.Commands;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Notifications;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Triage;
using MockQueryable.Moq;
using Moq;
using StaffEntity = HealthERP.Domain.StaffMembers.StaffMembers;

namespace HealthERP.Application.UnitTests.Notifications;

public class WorkflowNotificationTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<ICurrentUserService> _actor = new();
    private readonly Mock<IPrivateNotificationPublisher> _publisher = new();
    private readonly List<Notification> _notifications = [];
    private bool _saved;

    public WorkflowNotificationTests()
    {
        _actor.SetupGet(u => u.UserId).Returns(_actorId.ToString());
        var set = _notifications.BuildMockDbSet();
        set.Setup(s => s.Add(It.IsAny<Notification>())).Callback<Notification>(_notifications.Add);
        _context.Setup(c => c.Notifications).Returns(set.Object);
        _context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => _saved = true).ReturnsAsync(1);
        _publisher.Setup(p => p.PublishAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => {
                Assert.True(_saved);
                Assert.Equal(_actorId, n.TargetUserId);
                Assert.Same(Assert.Single(_notifications), n);
            }).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task AppointmentNotification_BelongsToActor_PublishesAfterSave_WithoutClinicalContent()
    {
        _context.Setup(c => c.Appointments).Returns(new List<Appointment>().BuildMockDbSet().Object);
        var appointments = new Mock<IAppointmentNotificationService>();
        appointments.Setup(s => s.NotifyAppointmentChangedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await new BookAppointmentCommandHandler(_context.Object, appointments.Object, _actor.Object, _publisher.Object)
            .Handle(new(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2),
                "PRIVATE_CLINICAL_REASON", "PRIVATE_NOTES"), default);
        Assert.DoesNotContain("PRIVATE", Assert.Single(_notifications).Message);
        AssertPublishedOnce();
        appointments.Verify(s => s.NotifyAppointmentChangedAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StaffNotification_BelongsToActor_AndPublishesAfterSave()
    {
        _context.Setup(c => c.Staff).Returns(new List<StaffEntity>().BuildMockDbSet().Object);
        await new CreateStaffCommandHandler(_context.Object, _actor.Object, _publisher.Object)
            .Handle(new("Test", "Staff", "test@example.invalid", "1234567890", "General Medicine", "LICENSE-1", "OutPatient"), default);
        AssertPublishedOnce();
    }

    [Fact]
    public async Task TriageNotification_BelongsToActor_PublishesAfterSave_WithoutPatientDetails()
    {
        var patient = Patient.Register("MRN-NOTIFY", "PRIVATE_FIRST", "PRIVATE_LAST", new DateOnly(1990, 1, 1));
        _context.Setup(c => c.Patients).Returns(new List<Patient> { patient }.BuildMockDbSet().Object);
        _context.Setup(c => c.TriageRecords).Returns(new List<TriageRecord>().BuildMockDbSet().Object);
        await new CreateTriageRecordCommandHandler(_context.Object, _actor.Object, _publisher.Object)
            .Handle(new(patient.Id.Value, "PRIVATE_COMPLAINT", "120/80", 72, 98, 37m, 16,
                TriageSeverity.Level2_Emergent, "Test Nurse"), default);
        Assert.DoesNotContain("PRIVATE", Assert.Single(_notifications).Message);
        Assert.DoesNotContain("SpO2", _notifications[0].Message);
        AssertPublishedOnce();
    }

    private void AssertPublishedOnce() => _publisher.Verify(p => p.PublishAsync(It.IsAny<Notification>(),
        It.IsAny<CancellationToken>()), Times.Once);
}
