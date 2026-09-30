using HealthERP.Application.Reports.Queries.GetExecutiveDashboardMetrics;
using HealthERP.Application.Common.Interfaces;
using Moq;
using MockQueryable.Moq;
using HealthERP.Domain.Billing;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Pharmacy;
using HealthERP.Domain.Laboratory;
using Xunit;

namespace HealthERP.Application.UnitTests.Reports;

public class GetExecutiveDashboardMetricsQueryHandlerTests
{
    [Fact]
    public async Task Handle_EmptyData_ShouldReturnZeroedMetrics()
    {
        // Arrange
        var contextMock = new Mock<IApplicationDbContext>();

        var invoices = new List<Invoice>().BuildMockDbSet();
        var patients = new List<Patient>().BuildMockDbSet();
        var appts = new List<Appointment>().BuildMockDbSet();
        var prescriptions = new List<Prescription>().BuildMockDbSet();
        var labOrders = new List<LabOrder>().BuildMockDbSet();

        contextMock.Setup(c => c.Invoices).Returns(invoices.Object);
        contextMock.Setup(c => c.Patients).Returns(patients.Object);
        contextMock.Setup(c => c.Appointments).Returns(appts.Object);
        contextMock.Setup(c => c.Prescriptions).Returns(prescriptions.Object);
        contextMock.Setup(c => c.LabOrders).Returns(labOrders.Object);

        var handler = new GetExecutiveDashboardMetricsQueryHandler(contextMock.Object);

        // Act
        var result = await handler.Handle(new GetExecutiveDashboardMetricsQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalRevenue);
        Assert.Equal(0, result.TotalCollected);
        Assert.Equal(0, result.TotalPatients);
        Assert.Equal(6, result.MonthlyRevenueTrend.Count);
    }
}
