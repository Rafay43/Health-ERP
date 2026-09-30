using HealthERP.Application.Admin.Queries.GetAuditLogs;
using HealthERP.Application.Common.Interfaces;
using Moq;
using MockQueryable.Moq;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Pharmacy;
using HealthERP.Domain.Laboratory;
using HealthERP.Domain.Billing;
using Xunit;

namespace HealthERP.Application.UnitTests.Admin;

public class GetAuditLogsQueryHandlerTests
{
    [Fact]
    public async Task Handle_EmptyDatabase_ShouldReturnEmptyLogs()
    {
        // Arrange
        var contextMock = new Mock<IApplicationDbContext>();

        var patients = new List<Patient>().BuildMockDbSet();
        var prescriptions = new List<Prescription>().BuildMockDbSet();
        var labOrders = new List<LabOrder>().BuildMockDbSet();
        var invoices = new List<Invoice>().BuildMockDbSet();
        var notes = new List<ClinicalNote>().BuildMockDbSet();

        contextMock.Setup(c => c.Patients).Returns(patients.Object);
        contextMock.Setup(c => c.Prescriptions).Returns(prescriptions.Object);
        contextMock.Setup(c => c.LabOrders).Returns(labOrders.Object);
        contextMock.Setup(c => c.Invoices).Returns(invoices.Object);
        contextMock.Setup(c => c.ClinicalNotes).Returns(notes.Object);

        var handler = new GetAuditLogsQueryHandler(contextMock.Object);

        // Act
        var result = await handler.Handle(new GetAuditLogsQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
