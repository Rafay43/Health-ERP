using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Reports.Models;
using System.Globalization;

namespace HealthERP.Application.Reports.Queries.GetExecutiveDashboardMetrics;

public class GetExecutiveDashboardMetricsQueryHandler : IRequestHandler<GetExecutiveDashboardMetricsQuery, ExecutiveDashboardMetricsDto>
{
    private readonly IApplicationDbContext _context;

    public GetExecutiveDashboardMetricsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExecutiveDashboardMetricsDto> Handle(GetExecutiveDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        // 1. Core Financial Totals
        var invoices = await _context.Invoices.AsNoTracking().ToListAsync(cancellationToken);
        var totalRevenue = invoices.Sum(i => i.TotalAmount);
        var totalCollected = invoices.Sum(i => i.PaidAmount);
        var totalOutstanding = invoices.Sum(i => i.BalanceDue);

        // 2. Entity Counts
        var totalPatients = await _context.Patients.AsNoTracking().CountAsync(cancellationToken);
        var totalAppointments = await _context.Appointments.AsNoTracking().CountAsync(cancellationToken);
        var totalPrescriptions = await _context.Prescriptions.AsNoTracking().CountAsync(cancellationToken);
        var totalLabTests = await _context.LabOrders.AsNoTracking().CountAsync(cancellationToken);

        // 3. Monthly Revenue Trend (Last 6 Months)
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-5);
        var startOfRange = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var monthlyGrouped = invoices
            .Where(i => i.IssueDateUtc >= startOfRange)
            .GroupBy(i => new { i.IssueDateUtc.Year, i.IssueDateUtc.Month })
            .ToDictionary(
                g => $"{g.Key.Year}-{g.Key.Month:D2}",
                g => new { Billed = g.Sum(x => x.TotalAmount), Collected = g.Sum(x => x.PaidAmount) }
            );

        var monthlyTrend = new List<MonthlyRevenueItemDto>();
        for (int i = 5; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddMonths(-i);
            var key = $"{date.Year}-{date.Month:D2}";
            var monthName = date.ToString("MMM yyyy", CultureInfo.InvariantCulture);

            if (monthlyGrouped.TryGetValue(key, out var data))
            {
                monthlyTrend.Add(new MonthlyRevenueItemDto(monthName, data.Billed, data.Collected));
            }
            else
            {
                monthlyTrend.Add(new MonthlyRevenueItemDto(monthName, 0m, 0m));
            }
        }

        // 4. Appointment Status Breakdown
        var appts = await _context.Appointments.AsNoTracking().ToListAsync(cancellationToken);
        var apptBreakdown = appts
            .GroupBy(a => a.Status.ToString())
            .Select(g => new AppointmentStatusCountDto(g.Key, g.Count()))
            .ToList();

        // 5. Top Prescribed Drugs
        var prescriptions = await _context.Prescriptions.AsNoTracking().ToListAsync(cancellationToken);
        var topDrugs = prescriptions
            .GroupBy(p => p.MedicationName)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new TopItemCountDto(g.Key, g.Count()))
            .ToList();

        // 6. Top Lab Tests Executed
        var labOrders = await _context.LabOrders.AsNoTracking().ToListAsync(cancellationToken);
        var topLabTests = labOrders
            .GroupBy(o => o.TestName)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new TopItemCountDto(g.Key, g.Count()))
            .ToList();

        return new ExecutiveDashboardMetricsDto(
            totalRevenue,
            totalCollected,
            totalOutstanding,
            totalPatients,
            totalAppointments,
            totalPrescriptions,
            totalLabTests,
            monthlyTrend,
            apptBreakdown,
            topDrugs,
            topLabTests
        );
    }
}
