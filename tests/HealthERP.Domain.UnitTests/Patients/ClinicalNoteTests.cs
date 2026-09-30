using HealthERP.Domain.Patients;
using Xunit;

namespace HealthERP.Domain.UnitTests.Patients;

public class ClinicalNoteTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateClinicalNote()
    {
        // Arrange
        var patientId = PatientId.New();

        // Act
        var note = ClinicalNote.Create(
            patientId,
            "Dr. Sarah Jenkins",
            "Fever and cough",
            "Upper Respiratory Tract Infection",
            "Rest and oral hydration",
            "120/80",
            72,
            37.2m,
            70m);

        // Assert
        Assert.NotNull(note);
        Assert.Equal(patientId, note.PatientId);
        Assert.Equal("Dr. Sarah Jenkins", note.DoctorName);
        Assert.Equal("120/80", note.BloodPressure);
        Assert.Equal(72, note.HeartRateBpm);
        Assert.Equal(37.2m, note.TemperatureCelsius);
    }

    [Fact]
    public void Create_EmptyDoctorName_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ClinicalNote.Create(PatientId.New(), "", "Chief Complaint", "Diagnosis", "Notes"));
    }
}
