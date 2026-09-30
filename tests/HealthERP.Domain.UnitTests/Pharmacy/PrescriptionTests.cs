using HealthERP.Domain.Patients;
using HealthERP.Domain.Pharmacy;
using Xunit;

namespace HealthERP.Domain.UnitTests.Pharmacy;

public class PrescriptionTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreatePendingPrescription()
    {
        // Arrange
        var patientId = PatientId.New();

        // Act
        var rx = Prescription.Create(
            patientId,
            null,
            "Dr. Smith",
            "Amoxicillin 500mg",
            "500mg",
            "Twice daily",
            7,
            1,
            "Take with food");

        // Assert
        Assert.NotNull(rx);
        Assert.Equal(PrescriptionStatus.Pending, rx.Status);
        Assert.Equal("Dr. Smith", rx.DoctorName);
        Assert.Equal("Amoxicillin 500mg", rx.MedicationName);
        Assert.Null(rx.DispensedDateUtc);
    }

    [Fact]
    public void Dispense_PendingPrescription_ShouldUpdateStatusAndTimestamp()
    {
        // Arrange
        var rx = Prescription.Create(
            PatientId.New(),
            null,
            "Dr. Smith",
            "Paracetamol",
            "500mg",
            "Once daily",
            5,
            0,
            null);

        // Act
        rx.Dispense();

        // Assert
        Assert.Equal(PrescriptionStatus.Dispensed, rx.Status);
        Assert.NotNull(rx.DispensedDateUtc);
    }

    [Fact]
    public void Dispense_AlreadyDispensed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var rx = Prescription.Create(
            PatientId.New(),
            null,
            "Dr. Smith",
            "Paracetamol",
            "500mg",
            "Once daily",
            5,
            0,
            null);
        rx.Dispense();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => rx.Dispense());
    }
}
