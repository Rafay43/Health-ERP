using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Triage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Notifications;

namespace HealthERP.Application.Triage.Commands;

public record CreateTriageRecordCommand(
    Guid PatientId,
    string ChiefComplaint,
    string BloodPressure,
    int HeartRateBpm,
    int OxygenSaturationSpO2,
    decimal BodyTemperatureCelsius,
    int RespiratoryRateBpm,
    TriageSeverity Severity,
    string TriageNurseName,
    string? AssignedBay = null
) : IRequest<Guid>;

public class CreateTriageRecordCommandHandler : IRequestHandler<CreateTriageRecordCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPrivateNotificationPublisher _publisher;

    public CreateTriageRecordCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IPrivateNotificationPublisher publisher)
    {
        _context = context;
        _currentUser = currentUser;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(CreateTriageRecordCommand request, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(request.PatientId);

        var patientExists = await _context.Patients.AnyAsync(p => p.Id == patientId, cancellationToken);
        if (!patientExists)
        {
            throw new KeyNotFoundException($"Patient with ID '{request.PatientId}' was not found.");
        }

        var triageRecord = TriageRecord.Create(
            patientId,
            request.ChiefComplaint,
            request.BloodPressure,
            request.HeartRateBpm,
            request.OxygenSaturationSpO2,
            request.BodyTemperatureCelsius,
            request.RespiratoryRateBpm,
            request.Severity,
            request.TriageNurseName,
            request.AssignedBay
        );

        _context.TriageRecords.Add(triageRecord);

        var isCritical = request.Severity == TriageSeverity.Level1_Resuscitation || request.Severity == TriageSeverity.Level2_Emergent;

        var notification = HealthERP.Domain.Notifications.Notification.Create(
            $"Emergency Triage Alert (ESI Level {(int)request.Severity})",
            "A new emergency triage record requires attention. Open emergency for details.",
            HealthERP.Domain.Notifications.NotificationType.EmergencyAlert,
            isCritical ? HealthERP.Domain.Notifications.NotificationSeverity.Critical : HealthERP.Domain.Notifications.NotificationSeverity.Warning,
            "/emergency",
            targetUserId: _currentUser.GetRequiredId()
        );
        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAsync(notification, cancellationToken);

        return triageRecord.Id.Value;
    }
}
