using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Patients.Models;
using HealthERP.Application.Pharmacy.Models;
using HealthERP.Application.Laboratory.Models;
using HealthERP.Domain.Patients;

namespace HealthERP.Application.Patients.Queries.GetPatientEhr;

public class GetPatientEhrQueryHandler : IRequestHandler<GetPatientEhrQuery, PatientEhrDto>
{
    private readonly IApplicationDbContext _context;

    public GetPatientEhrQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PatientEhrDto> Handle(GetPatientEhrQuery request, CancellationToken cancellationToken)
    {
        var pId = new PatientId(request.PatientId);
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pId, cancellationToken);

        if (patient == null)
            throw new KeyNotFoundException($"Patient with ID '{request.PatientId}' was not found.");

        var age = DateTime.UtcNow.Year - patient.DateOfBirth.Year;
        if (patient.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-age))) age--;

        // 1. Load Clinical Notes
        var clinicalNotes = await _context.ClinicalNotes
            .AsNoTracking()
            .Where(n => n.PatientId == pId)
            .OrderByDescending(n => n.NoteDateUtc)
            .ToListAsync(cancellationToken);

        var latestNoteWithVitals = clinicalNotes.FirstOrDefault(n =>
            !string.IsNullOrEmpty(n.BloodPressure) || n.HeartRateBpm.HasValue || n.TemperatureCelsius.HasValue || n.WeightKg.HasValue);

        VitalsSummaryDto? latestVitals = latestNoteWithVitals != null ? new VitalsSummaryDto(
            latestNoteWithVitals.BloodPressure,
            latestNoteWithVitals.HeartRateBpm,
            latestNoteWithVitals.TemperatureCelsius,
            latestNoteWithVitals.WeightKg,
            latestNoteWithVitals.NoteDateUtc
        ) : null;

        // 2. Load Prescriptions
        var prescriptions = await _context.Prescriptions
            .Include(p => p.Drug)
            .AsNoTracking()
            .Where(p => p.PatientId == pId)
            .OrderByDescending(p => p.PrescribedDateUtc)
            .ToListAsync(cancellationToken);

        var activeMedications = prescriptions
            .Where(p => p.Status.ToString() == "Dispensed" || p.Status.ToString() == "Pending")
            .Select(p => new PrescriptionDto(
                p.Id.Value,
                p.PatientId.Value,
                $"{patient.FirstName} {patient.LastName}",
                p.DrugId?.Value,
                p.Drug != null ? p.Drug.Name : p.MedicationName,
                p.DoctorName,
                p.MedicationName,
                p.Dosage,
                p.Frequency,
                p.DurationDays,
                p.Refills,
                p.Instructions,
                p.Status.ToString(),
                p.PrescribedDateUtc,
                p.DispensedDateUtc
            )).ToList();

        // 3. Load Lab Orders
        var labOrders = await _context.LabOrders
            .Include(o => o.LabTest)
            .AsNoTracking()
            .Where(o => o.PatientId == pId)
            .OrderByDescending(o => o.OrderedDateUtc)
            .ToListAsync(cancellationToken);

        var pendingLabResults = labOrders
            .Where(o => o.Status.ToString() != "Completed" && o.Status.ToString() != "Cancelled")
            .Select(o => new LabOrderDto(
                o.Id.Value,
                o.PatientId.Value,
                $"{patient.FirstName} {patient.LastName}",
                o.LabTestId?.Value,
                o.TestName,
                o.TestCode,
                o.OrderingDoctorName,
                o.Priority,
                o.Status.ToString(),
                o.OrderedDateUtc,
                o.ResultValue,
                o.ReferenceRange,
                o.Unit,
                o.Remarks,
                o.PerformedBy,
                o.CompletedDateUtc,
                o.IsAbnormal
            )).ToList();

        // 4. Load Appointments
        var appointments = await _context.Appointments
            .Include(a => a.Staff)
            .AsNoTracking()
            .Where(a => a.PatientId == pId)
            .OrderByDescending(a => a.StartDateTimeUtc)
            .ToListAsync(cancellationToken);

        // 5. Load Invoices
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.PatientId == pId)
            .OrderByDescending(i => i.IssueDateUtc)
            .ToListAsync(cancellationToken);

        // Construct Timeline Events Array
        var timeline = new List<EhrTimelineEventDto>();

        foreach (var note in clinicalNotes)
        {
            timeline.Add(new EhrTimelineEventDto(
                note.Id.Value,
                "ClinicalNote",
                note.NoteDateUtc,
                $"Clinical Encounter - {note.Diagnosis}",
                $"Chief Complaint: {note.ChiefComplaint}. Notes: {note.Notes}",
                "Recorded",
                "info",
                "pi pi-file-edit",
                new { doctor = note.DoctorName, bp = note.BloodPressure, pulse = note.HeartRateBpm }
            ));
        }

        foreach (var rx in prescriptions)
        {
            timeline.Add(new EhrTimelineEventDto(
                rx.Id.Value,
                "Prescription",
                rx.PrescribedDateUtc,
                $"Prescription: {rx.MedicationName} ({rx.Dosage})",
                $"Prescribed by Dr. {rx.DoctorName}. Frequency: {rx.Frequency} for {rx.DurationDays} days.",
                rx.Status.ToString(),
                rx.Status.ToString() == "Dispensed" ? "success" : "warn",
                "pi pi-heart",
                new { rx.MedicationName, rx.Dosage, rx.Refills }
            ));
        }

        foreach (var lab in labOrders)
        {
            timeline.Add(new EhrTimelineEventDto(
                lab.Id.Value,
                "LabOrder",
                lab.OrderedDateUtc,
                $"Diagnostic Test: {lab.TestName} ({lab.TestCode})",
                lab.Status.ToString() == "Completed"
                    ? $"Result: {lab.ResultValue} {lab.Unit} (Ref: {lab.ReferenceRange}). Tech: {lab.PerformedBy}"
                    : $"Ordered by Dr. {lab.OrderingDoctorName}. Priority: {lab.Priority}",
                lab.Status.ToString(),
                lab.IsAbnormal ? "danger" : (lab.Status.ToString() == "Completed" ? "success" : "warn"),
                "pi pi-compass",
                new { lab.TestName, lab.ResultValue, lab.IsAbnormal }
            ));
        }

        foreach (var appt in appointments)
        {
            var docName = appt.Staff != null ? $"{appt.Staff.FirstName} {appt.Staff.LastName}" : "Assigned Physician";
            timeline.Add(new EhrTimelineEventDto(
                appt.Id.Value,
                "Appointment",
                appt.StartDateTimeUtc,
                $"Consultation Appointment with Dr. {docName}",
                $"Reason: {appt.Reason}. Status: {appt.Status}",
                appt.Status.ToString(),
                appt.Status.ToString() == "Completed" ? "success" : "info",
                "pi pi-calendar",
                new { docName, reason = appt.Reason }
            ));
        }

        foreach (var inv in invoices)
        {
            timeline.Add(new EhrTimelineEventDto(
                inv.Id.Value,
                "Invoice",
                inv.IssueDateUtc,
                $"Billing Invoice #{inv.InvoiceNumber}",
                $"Total: ${inv.TotalAmount:N2}. Paid: ${inv.PaidAmount:N2}. Balance: ${inv.BalanceDue:N2}",
                inv.Status.ToString(),
                inv.Status.ToString() == "Paid" ? "success" : "warn",
                "pi pi-dollar",
                new { inv.InvoiceNumber, inv.TotalAmount, inv.BalanceDue }
            ));
        }

        var sortedTimeline = timeline.OrderByDescending(t => t.TimestampUtc).ToList();

        return new PatientEhrDto(
            patient.Id.Value,
            patient.MedicalRecordNumber,
            patient.FirstName,
            patient.LastName,
            patient.DateOfBirth,
            age,
            patient.Gender,
            patient.PhoneNumber,
            patient.Email,
            latestVitals,
            activeMedications,
            pendingLabResults,
            sortedTimeline
        );
    }
}
