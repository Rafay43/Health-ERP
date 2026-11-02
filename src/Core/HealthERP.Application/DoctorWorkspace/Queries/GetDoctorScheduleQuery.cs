using HealthERP.Application.Common.Configurations;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.DoctorWorkspace.Models;
using HealthERP.Application.StaffMembers;
using HealthERP.Domain.Appointments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.DoctorWorkspace.Queries;

public sealed record GetDoctorScheduleQuery(DateOnly? Date = null) : IRequest<DoctorScheduleDto>;

public sealed class GetDoctorScheduleQueryHandler(IApplicationDbContext db, ICurrentStaffService currentStaff,
    TimeProvider clock, FacilityTimeZone facility) : IRequestHandler<GetDoctorScheduleQuery, DoctorScheduleDto>
{
    public async Task<DoctorScheduleDto> Handle(GetDoctorScheduleQuery request, CancellationToken ct)
    {
        var current = await currentStaff.GetCurrentDoctorAsync(ct);
        var now = clock.GetUtcNow();
        var date = request.Date ?? facility.GetDate(now);
        var (start, end) = facility.GetUtcWindow(date);
        var doctor = await db.Staff.AsNoTracking().Where(s => s.Id == current.StaffId && s.IsActive)
            .Select(s => new ScheduleDoctorDto(s.FirstName + " " + s.LastName, s.Specialty, s.Department))
            .SingleOrDefaultAsync(ct) ?? throw new StaffAccessDeniedException();

        // Left join keeps the real appointment even when its Patient is soft deleted.
        // Patient query filters still apply; unavailable identity stays null, without fabricated names.
        var appointments = await (from a in db.Appointments.AsNoTracking()
            where a.StaffId == current.StaffId && !a.IsDeleted && a.StartDateTimeUtc >= start && a.StartDateTimeUtc < end
            join p in db.Patients.AsNoTracking() on a.PatientId equals p.Id into patients
            from p in patients.DefaultIfEmpty()
            orderby a.StartDateTimeUtc, a.Id
            select new DoctorScheduleAppointmentDto(a.Id.Value, a.PatientId.Value,
                p == null ? null : p.MedicalRecordNumber, p == null ? null : p.FirstName + " " + p.LastName,
                a.StartDateTimeUtc, a.EndDateTimeUtc, a.Status.ToString(), a.Reason)).ToListAsync(ct);

        int Count(AppointmentStatus status) => appointments.Count(a => a.Status == status.ToString());
        var summary = new DoctorScheduleSummaryDto(appointments.Count, Count(AppointmentStatus.Scheduled),
            Count(AppointmentStatus.Confirmed), Count(AppointmentStatus.Completed), Count(AppointmentStatus.Cancelled), Count(AppointmentStatus.NoShow));
        var next = appointments.FirstOrDefault(a => a.StartDateTimeUtc >= now.UtcDateTime
            && (a.Status == nameof(AppointmentStatus.Scheduled) || a.Status == nameof(AppointmentStatus.Confirmed)));
        return new(date, facility.Zone.Id, doctor, summary, next, appointments);
    }
}
