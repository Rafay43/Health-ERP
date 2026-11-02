using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.StaffMembers.Models;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.DoctorWorkspace;

internal static class DoctorPatientAccess
{
    // Only accepts the trusted context obtained by the handler from GetCurrentDoctorAsync.
    internal static Task<bool> HasAccessAsync(IApplicationDbContext db, CurrentStaffContext doctor, PatientId patientId, CancellationToken ct)
        => db.Appointments.AsNoTracking().AnyAsync(a => a.StaffId == doctor.StaffId && a.PatientId == patientId && !a.IsDeleted
            && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Completed), ct);
}
