using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Patients.Commands.RegisterPatient;
using HealthERP.Application.Patients.Commands.UpdatePatient;
using HealthERP.Domain.Patients;
using Moq;
using MockQueryable.Moq;
using Shouldly;
using Xunit;

namespace HealthERP.Application.UnitTests.Patients;

public class PatientGenderTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;

    public PatientGenderTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
    }

    [Theory]
    [InlineData("Male")]
    [InlineData("Female")]
    [InlineData("Other")]
    [InlineData("Prefer not to say")]
    [InlineData("male")]
    [InlineData(" FEMALE ")]
    public async Task RegisterPatient_WithValidGender_Succeeds(string genderInput)
    {
        // Arrange
        var patients = new List<Patient>().BuildMockDbSet();
        _contextMock.Setup(c => c.Patients).Returns(patients.Object);

        var handler = new RegisterPatientCommandHandler(_contextMock.Object);
        var command = new RegisterPatientCommand(
            "MRN-1001",
            "Alice",
            "Smith",
            new DateOnly(1990, 5, 15),
            genderInput,
            "alice@example.com",
            "+1-555-0100");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBe(Guid.Empty);
        _contextMock.Verify(c => c.Patients.Add(It.Is<Patient>(p =>
            p.Email == "alice@example.com" &&
            p.PhoneNumber == "+1-555-0100" &&
            (p.Gender == "Male" || p.Gender == "Female" || p.Gender == "Other" || p.Gender == "Prefer not to say")
        )), Times.Once);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("InvalidText")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("Robot")]
    public async Task RegisterPatient_WithInvalidGender_ThrowsArgumentException(string invalidGender)
    {
        // Arrange
        var patients = new List<Patient>().BuildMockDbSet();
        _contextMock.Setup(c => c.Patients).Returns(patients.Object);

        var handler = new RegisterPatientCommandHandler(_contextMock.Object);
        var command = new RegisterPatientCommand(
            "MRN-1002",
            "Bob",
            "Jones",
            new DateOnly(1985, 8, 20),
            invalidGender,
            "bob@example.com",
            null);

        // Act & Assert
        var ex = await Should.ThrowAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        ex.ParamName.ShouldBe("Gender");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task RegisterPatient_WithInvalidEmail_ThrowsArgumentException(string invalidEmail)
    {
        // Arrange
        var patients = new List<Patient>().BuildMockDbSet();
        _contextMock.Setup(c => c.Patients).Returns(patients.Object);

        var handler = new RegisterPatientCommandHandler(_contextMock.Object);
        var command = new RegisterPatientCommand(
            "MRN-1003",
            "Charlie",
            "Brown",
            new DateOnly(1992, 12, 1),
            "Male",
            invalidEmail,
            null);

        // Act & Assert
        var ex = await Should.ThrowAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        ex.ParamName.ShouldBe("Email");
    }

    [Fact]
    public async Task UpdatePatient_WithValidGenderAndContact_UpdatesPatientRecord()
    {
        // Arrange
        var existingPatient = Patient.Register("MRN-2001", "David", "Miller", new DateOnly(1988, 3, 10), "Male", "david@old.com", "+1-111");
        var patientsList = new List<Patient> { existingPatient };
        var patientsDbSet = patientsList.BuildMockDbSet();
        _contextMock.Setup(c => c.Patients).Returns(patientsDbSet.Object);

        var handler = new UpdatePatientCommandHandler(_contextMock.Object);
        var command = new UpdatePatientCommand(
            existingPatient.Id.Value,
            "David",
            "Miller",
            "Female",
            "david.new@example.com",
            "+1-222-333");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        existingPatient.Gender.ShouldBe("Female");
        existingPatient.Email.ShouldBe("david.new@example.com");
        existingPatient.PhoneNumber.ShouldBe("+1-222-333");
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void LegacyPatientRecord_WithoutGender_LoadsSafelyWithoutException()
    {
        // Arrange & Act
        var legacy = Patient.Register("MRN-9999", "Legacy", "Patient", new DateOnly(1970, 1, 1), null, null, null);

        // Assert
        legacy.Gender.ShouldBeNull();
        legacy.Email.ShouldBeNull();
        legacy.PhoneNumber.ShouldBeNull();
        legacy.FirstName.ShouldBe("Legacy");
    }
}
