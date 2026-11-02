using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientPortal.Queries.GetMyProfile;
using HealthERP.Application.PatientPortal.Queries.GetMyAppointments;
using HealthERP.Application.PatientPortal.Queries.GetMyHealthRecord;
using HealthERP.Application.PatientPortal.Queries.GetMyPrescriptions;
using HealthERP.Application.PatientPortal.Queries.GetMyLabResults;
using HealthERP.Application.PatientPortal.Queries.GetMyBilling;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Pharmacy;
using HealthERP.Domain.Laboratory;
using HealthERP.Domain.Billing;
using HealthERP.Domain.StaffMembers;
using HealthERP.SharedKernel;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace HealthERP.Application.UnitTests.PatientPortal;

public class PatientPortalUnitTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ICurrentPatientService> _currentPatientServiceMock;

    public PatientPortalUnitTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _currentPatientServiceMock = new Mock<ICurrentPatientService>();
    }

    [Fact]
    public async Task GetMyProfile_UnlinkedPatient_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        _currentPatientServiceMock
            .Setup(s => s.GetCurrentPatientAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var handler = new GetMyProfileQueryHandler(_currentPatientServiceMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetMyProfileQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task GetMyProfile_LinkedPatient_ShouldReturnOnlyPatientData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var patient = Patient.Register("MRN-001", "Jane", "Doe", new DateOnly(1995, 5, 15));
        patient.LinkUserId(userId);
        patient.UpdateContactInfo("03001234567", "jane@example.com");

        _currentPatientServiceMock
            .Setup(s => s.GetCurrentPatientAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var handler = new GetMyProfileQueryHandler(_currentPatientServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetMyProfileQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MRN-001", result.Mrn);
        Assert.Equal("Jane", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("Jane Doe", result.DisplayName);
    }

    [Fact]
    public async Task GetMyAppointments_ShouldReturnOnlyCurrentPatientAppointments()
    {
        // Arrange
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        var patientA = Patient.Register("MRN-001", "Patient", "A", new DateOnly(1990, 1, 1));
        patientA.LinkUserId(userAId);

        var patientB = Patient.Register("MRN-002", "Patient", "B", new DateOnly(1992, 2, 2));
        patientB.LinkUserId(userBId);

        var staffId = StaffId.New();

        var apptA = Appointment.Schedule(patientA.Id, staffId, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddMinutes(30), "General Consultation", "Regular Checkup");
        var apptB = Appointment.Schedule(patientB.Id, staffId, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddMinutes(30), "Cardiology", "Heart Checkup");

        var appointments = new List<Appointment> { apptA, apptB }.BuildMockDbSet();
        _contextMock.Setup(c => c.Appointments).Returns(appointments.Object);

        _currentPatientServiceMock
            .Setup(s => s.GetCurrentPatientAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(patientA);

        var handler = new GetMyAppointmentsQueryHandler(_contextMock.Object, _currentPatientServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetMyAppointmentsQuery(1, 10, null), CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("General Consultation", result[0].Reason);
    }

    [Fact]
    public async Task GetMyLabResults_CompletedOnly_ShouldExcludePendingOrders()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var patient = Patient.Register("MRN-001", "Jane", "Doe", new DateOnly(1995, 5, 15));
        patient.LinkUserId(userId);

        var pendingOrder = LabOrder.Create(patient.Id, null, "Dr. Smith", "CBC", "CBC01");
        
        var completedOrder = LabOrder.Create(patient.Id, null, "Dr. Smith", "Lipid Panel", "LIP01");
        completedOrder.RecordResult("Cholesterol: 180 mg/dL", "Normal", "Tech 1");

        var labOrders = new List<LabOrder> { pendingOrder, completedOrder }.BuildMockDbSet();
        _contextMock.Setup(c => c.LabOrders).Returns(labOrders.Object);

        _currentPatientServiceMock
            .Setup(s => s.GetCurrentPatientAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var handler = new GetMyLabResultsQueryHandler(_contextMock.Object, _currentPatientServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetMyLabResultsQuery(1, 10, CompletedOnly: true), CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("Lipid Panel", result[0].TestName);
        Assert.Equal("Completed", result[0].Status);
    }
}
