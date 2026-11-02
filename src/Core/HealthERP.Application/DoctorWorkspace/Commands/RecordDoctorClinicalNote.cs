using System.Text.Json.Serialization;
using FluentValidation;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Patients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.DoctorWorkspace.Commands;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DoctorClinicalNoteInput(string? ChiefComplaint = null, string? Diagnosis = null, string? Notes = null,
    string? BloodPressure = null, int? HeartRateBpm = null, decimal? TemperatureCelsius = null, decimal? WeightKg = null);

// Deliberately validates the input after access resolution, rather than exposing validation before authorization.
public sealed class DoctorClinicalNoteInputValidator : AbstractValidator<DoctorClinicalNoteInput>
{
    public DoctorClinicalNoteInputValidator() => RuleFor(x => x).Custom((x, context) =>
    {
        foreach (var error in ClinicalNoteRecordingRules.Errors(x.ChiefComplaint, x.Diagnosis, x.Notes, x.BloodPressure, x.HeartRateBpm, x.TemperatureCelsius, x.WeightKg))
            context.AddFailure(error);
    });
}
public sealed record RecordedClinicalNoteDto(Guid NoteId, DateTime RecordedAtUtc);
public sealed record RecordDoctorClinicalNoteCommand(Guid PatientId, DoctorClinicalNoteInput Input) : IRequest<RecordedClinicalNoteDto>;

public sealed class RecordDoctorClinicalNoteCommandHandler(IApplicationDbContext db, ICurrentStaffService currentStaff,
    TimeProvider clock, IValidator<DoctorClinicalNoteInput> validator) : IRequestHandler<RecordDoctorClinicalNoteCommand, RecordedClinicalNoteDto>
{
    public async Task<RecordedClinicalNoteDto> Handle(RecordDoctorClinicalNoteCommand request, CancellationToken ct)
    {
        var doctor = await currentStaff.GetCurrentDoctorAsync(ct);
        var patientId = new PatientId(request.PatientId);
        if (!await DoctorPatientAccess.HasAccessAsync(db, doctor, patientId, ct)
            || !await db.Patients.AsNoTracking().AnyAsync(p => p.Id == patientId && !p.IsDeleted, ct))
            throw new KeyNotFoundException("Patient was not found.");
        await validator.ValidateAndThrowAsync(request.Input, ct);
        var staffName = await db.Staff.AsNoTracking().Where(s => s.Id == doctor.StaffId)
            .Select(s => s.FirstName + " " + s.LastName).SingleAsync(ct);
        var input = request.Input;
        var note = ClinicalNote.RecordByDoctor(patientId, doctor.StaffId, staffName, clock.GetUtcNow().UtcDateTime,
            input.ChiefComplaint, input.Diagnosis, input.Notes, input.BloodPressure, input.HeartRateBpm, input.TemperatureCelsius, input.WeightKg);
        db.ClinicalNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return new(note.Id.Value, note.NoteDateUtc);
    }
}
