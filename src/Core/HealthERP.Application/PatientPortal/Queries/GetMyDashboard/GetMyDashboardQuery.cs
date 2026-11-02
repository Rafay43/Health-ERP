using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientPortal.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.PatientPortal.Queries.GetMyDashboard;

public record GetMyDashboardQuery : IRequest<PatientDashboardDto>;

public class GetMyDashboardQueryHandler : IRequestHandler<GetMyDashboardQuery, PatientDashboardDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentPatientService _currentPatientService;

    public GetMyDashboardQueryHandler(
        IApplicationDbContext context,
        ICurrentPatientService currentPatientService)
    {
        _context = context;
        _currentPatientService = currentPatientService;
    }

    public async Task<PatientDashboardDto> Handle(GetMyDashboardQuery request, CancellationToken cancellationToken)
    {
        var patient = await _currentPatientService.GetCurrentPatientAsync(cancellationToken);

        if (patient == null)
        {
            throw new UnauthorizedAccessException("Patient portal activation is required to access patient dashboard.");
        }

        // 1. Profile Summary
        var age = DateTime.UtcNow.Year - patient.DateOfBirth.Year;
        if (patient.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-age))) age--;

        var profile = new PatientPortalProfileDto(
            patient.MedicalRecordNumber,
            patient.FirstName,
            patient.LastName,
            $"{patient.FirstName} {patient.LastName}".Trim(),
            patient.DateOfBirth,
            Math.Max(0, age),
            patient.Gender,
            patient.PhoneNumber,
            patient.Email);

        // 2. Next Upcoming Appointment
        var nextApptEntity = await _context.Appointments
            .Include(a => a.Staff)
            .Where(a => a.PatientId == patient.Id && !a.IsDeleted && a.StartDateTimeUtc >= DateTime.UtcNow && a.Status == HealthERP.Domain.Appointments.AppointmentStatus.Scheduled)
            .OrderBy(a => a.StartDateTimeUtc)
            .FirstOrDefaultAsync(cancellationToken);

        PatientAppointmentDto? nextAppointment = null;
        if (nextApptEntity != null)
        {
            nextAppointment = new PatientAppointmentDto(
                nextApptEntity.Id.Value,
                nextApptEntity.StartDateTimeUtc,
                nextApptEntity.EndDateTimeUtc,
                nextApptEntity.Staff != null ? $"Dr. {nextApptEntity.Staff.FirstName} {nextApptEntity.Staff.LastName}".Trim() : "Attending Doctor",
                nextApptEntity.Staff?.Specialty,
                nextApptEntity.Staff?.Department,
                nextApptEntity.Reason,
                nextApptEntity.Status.ToString());
        }

        // 3. Active Prescriptions Count
        var activePrescriptionCount = await _context.Prescriptions
            .CountAsync(p => p.PatientId == patient.Id && (p.Status == HealthERP.Domain.Pharmacy.PrescriptionStatus.Pending || p.Status == HealthERP.Domain.Pharmacy.PrescriptionStatus.Dispensed), cancellationToken);

        // 4. Recent Lab Results Count (Completed in last 30 days or pending)
        var recentLabResultCount = await _context.LabOrders
            .CountAsync(l => l.PatientId == patient.Id && l.Status == HealthERP.Domain.Laboratory.LabOrderStatus.Completed, cancellationToken);

        // 5. Outstanding Balance
        var outstandingBalance = await _context.Invoices
            .Where(i => i.PatientId == patient.Id && i.Status != HealthERP.Domain.Billing.InvoiceStatus.Paid)
            .SumAsync(i => i.TotalAmount - i.PaidAmount, cancellationToken);

        // 6. Latest Vitals
        var latestNote = await _context.ClinicalNotes
            .Where(c => c.PatientId == patient.Id && (c.BloodPressure != null || c.HeartRateBpm != null || c.TemperatureCelsius != null || c.WeightKg != null))
            .OrderByDescending(c => c.NoteDateUtc)
            .FirstOrDefaultAsync(cancellationToken);

        LatestVitalsSummaryDto? latestVitals = null;
        if (latestNote != null)
        {
            latestVitals = new LatestVitalsSummaryDto(
                latestNote.BloodPressure,
                latestNote.HeartRateBpm,
                latestNote.TemperatureCelsius,
                latestNote.WeightKg,
                latestNote.NoteDateUtc);
        }

        return new PatientDashboardDto(
            profile,
            nextAppointment,
            activePrescriptionCount,
            recentLabResultCount,
            outstandingBalance,
            latestVitals);
    }
}
