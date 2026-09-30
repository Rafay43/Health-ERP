using MediatR;
using HealthERP.Application.Pharmacy.Models;

namespace HealthERP.Application.Pharmacy.Commands.CreatePrescription;

public record CreatePrescriptionCommand(
    Guid PatientId,
    Guid? DrugId,
    string DoctorName,
    string MedicationName,
    string Dosage,
    string Frequency,
    int DurationDays,
    int Refills,
    string? Instructions
) : IRequest<PrescriptionDto>;
