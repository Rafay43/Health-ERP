using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Patients.Models;
using HealthERP.Domain.Patients;

namespace HealthERP.Application.Patients.Commands.AddClinicalNote;

public class AddClinicalNoteCommandHandler : IRequestHandler<AddClinicalNoteCommand, ClinicalNoteDto>
{
    private readonly IApplicationDbContext _context;

    public AddClinicalNoteCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ClinicalNoteDto> Handle(AddClinicalNoteCommand request, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(request.PatientId);
        var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient == null)
            throw new KeyNotFoundException($"Patient with ID '{request.PatientId}' was not found.");

        var note = ClinicalNote.Create(
            patientId,
            request.DoctorName,
            request.ChiefComplaint,
            request.Diagnosis,
            request.Notes,
            request.BloodPressure,
            request.HeartRateBpm,
            request.TemperatureCelsius,
            request.WeightKg);

        _context.ClinicalNotes.Add(note);
        await _context.SaveChangesAsync(cancellationToken);

        return new ClinicalNoteDto(
            note.Id.Value,
            note.PatientId.Value,
            note.DoctorName,
            note.ChiefComplaint,
            note.Diagnosis,
            note.Notes,
            note.BloodPressure,
            note.HeartRateBpm,
            note.TemperatureCelsius,
            note.WeightKg,
            note.NoteDateUtc);
    }
}
