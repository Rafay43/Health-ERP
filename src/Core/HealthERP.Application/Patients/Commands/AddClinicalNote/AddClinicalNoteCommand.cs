using MediatR;
using HealthERP.Application.Patients.Models;

namespace HealthERP.Application.Patients.Commands.AddClinicalNote;

public record AddClinicalNoteCommand(
    Guid PatientId,
    string DoctorName,
    string ChiefComplaint,
    string Diagnosis,
    string Notes,
    string? BloodPressure = null,
    int? HeartRateBpm = null,
    decimal? TemperatureCelsius = null,
    decimal? WeightKg = null
) : IRequest<ClinicalNoteDto>;
