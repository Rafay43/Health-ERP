using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.DoctorWorkspace.Models;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Laboratory;
using HealthERP.Domain.Patients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.DoctorWorkspace.Queries;

public sealed record GetDoctorPatientSnapshotQuery(Guid PatientId) : IRequest<DoctorPatientSnapshotDto>;

public sealed class GetDoctorPatientSnapshotQueryHandler(IApplicationDbContext db, ICurrentStaffService currentStaff,
    TimeProvider clock) : IRequestHandler<GetDoctorPatientSnapshotQuery, DoctorPatientSnapshotDto>
{
    private const int HistoryLimit = 5;
    private static KeyNotFoundException NotFound() => new("Patient snapshot was not found.");

    public async Task<DoctorPatientSnapshotDto> Handle(GetDoctorPatientSnapshotQuery request, CancellationToken ct)
    {
        var doctor = await currentStaff.GetCurrentDoctorAsync(ct);
        var patientId = new PatientId(request.PatientId);
        // V1: any-date qualifying relationship. Resource knowledge and legacy author text confer no access.
        var hasAccess = await DoctorPatientAccess.HasAccessAsync(db, doctor, patientId, ct);
        if (!hasAccess) throw NotFound();

        // No Patient identity or clinical data is read until the authorization query succeeds.
        var patient = await db.Patients.AsNoTracking().Where(p => p.Id == patientId && !p.IsDeleted)
            .Select(p => new SnapshotPatientDto(p.Id.Value, p.MedicalRecordNumber, p.FirstName + " " + p.LastName,
                p.DateOfBirth, p.Gender)).SingleOrDefaultAsync(ct) ?? throw NotFound();

        // Latest *single* recorded observation, possibly partial. Never merge unrelated observations or fill defaults.
        var vitals = await db.ClinicalNotes.AsNoTracking().Where(n => n.PatientId == patientId
            && ((n.BloodPressure != null && n.BloodPressure.Trim() != "") || n.HeartRateBpm.HasValue
                || n.TemperatureCelsius.HasValue || n.WeightKg.HasValue))
            .OrderByDescending(n => n.NoteDateUtc).ThenByDescending(n => n.Id)
            .Select(n => new SnapshotVitalsDto(n.BloodPressure, n.HeartRateBpm, n.TemperatureCelsius, n.WeightKg, n.NoteDateUtc))
            .FirstOrDefaultAsync(ct);

        var notes = await db.ClinicalNotes.AsNoTracking().Where(n => n.PatientId == patientId)
            .OrderByDescending(n => n.NoteDateUtc).ThenByDescending(n => n.Id).Take(HistoryLimit)
            .Select(n => new SnapshotClinicalNoteDto(n.Id.Value, n.NoteDateUtc, n.ChiefComplaint, n.Diagnosis, n.Notes,
                n.RecordedByStaff == null ? null : n.RecordedByStaff.FirstName + " " + n.RecordedByStaff.LastName)).ToListAsync(ct);
        var prescriptions = await db.Prescriptions.AsNoTracking().Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.PrescribedDateUtc).ThenByDescending(p => p.Id).Take(HistoryLimit)
            .Select(p => new SnapshotPrescriptionDto(p.Id.Value, p.PrescribedDateUtc, p.MedicationName, p.Dosage,
                p.Frequency, p.DurationDays, p.Refills, p.Instructions, p.Status.ToString(), p.DispensedDateUtc)).ToListAsync(ct);
        var labs = await db.LabOrders.AsNoTracking().Where(l => l.PatientId == patientId)
            .OrderByDescending(l => l.OrderedDateUtc).ThenByDescending(l => l.Id).Take(HistoryLimit)
            .Select(l => new SnapshotLabOrderDto(l.Id.Value, l.OrderedDateUtc, l.TestName, l.TestCode, l.Priority,
                l.Status.ToString(), l.Status == LabOrderStatus.Completed
                    ? new SnapshotLabResultDto(l.CompletedDateUtc, l.ResultValue, l.ReferenceRange, l.Unit, l.Remarks, l.IsAbnormal)
                    : null)).ToListAsync(ct);

        var now = clock.GetUtcNow().UtcDateTime;
        var admission = await db.Admissions.AsNoTracking().Where(a => a.PatientId == patientId
                && !a.IsDischarged && a.DischargeDateUtc == null && a.AdmissionDateUtc <= now)
            .OrderByDescending(a => a.AdmissionDateUtc).ThenByDescending(a => a.Id)
            .Select(a => new SnapshotAdmissionDto(a.Id.Value, a.AdmissionDateUtc, a.ChiefComplaint)).FirstOrDefaultAsync(ct);
        return new(patient, vitals, notes, prescriptions, labs, admission);
    }
}
