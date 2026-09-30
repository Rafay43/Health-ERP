using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Application.Admin.Queries.GetAuditLogs;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, List<AuditLogEntryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetAuditLogsQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<List<AuditLogEntryDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var userDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var userAccounts = await _identityService.GetUserAccountsAsync(cancellationToken);
            if (userAccounts != null)
            {
                foreach (var u in userAccounts)
                {
                    var fullName = $"{u.FirstName} {u.LastName}".Trim();
                    var displayName = !string.IsNullOrWhiteSpace(fullName)
                        ? fullName
                        : (!string.IsNullOrWhiteSpace(u.UserName) ? u.UserName : u.Email);

                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        userDict[u.Id.ToString()] = displayName;
                        if (!string.IsNullOrWhiteSpace(u.Email)) userDict[u.Email] = displayName;
                        if (!string.IsNullOrWhiteSpace(u.UserName)) userDict[u.UserName] = displayName;
                    }
                }
            }
        }
        catch
        {
            // Fallback if identity service fails or user mapping isn't available
        }

        string ResolveUserName(string? rawUser, string fallback = "System Admin")
        {
            if (string.IsNullOrWhiteSpace(rawUser)) return fallback;
            if (userDict.TryGetValue(rawUser, out var resolvedName)) return resolvedName;
            return rawUser;
        }

        var logs = new List<AuditLogEntryDto>();

        // 1. Audit Patients
        var patients = await _context.Patients.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var p in patients)
        {
            logs.Add(new AuditLogEntryDto(
                Guid.NewGuid(),
                "Patient",
                p.Id.Value.ToString(),
                p.ModifiedAtUtc.HasValue ? "Modified" : "Created",
                ResolveUserName(p.ModifiedBy ?? p.CreatedBy, "System Admin"),
                p.ModifiedAtUtc ?? p.CreatedAtUtc,
                $"Registered MRN: {p.MedicalRecordNumber} - {p.FirstName} {p.LastName}"
            ));
        }

        // 2. Audit Prescriptions
        var prescriptions = await _context.Prescriptions.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var rx in prescriptions)
        {
            logs.Add(new AuditLogEntryDto(
                Guid.NewGuid(),
                "Prescription",
                rx.Id.Value.ToString(),
                rx.Status.ToString() == "Dispensed" ? "Dispensed" : "Created",
                ResolveUserName(rx.ModifiedBy ?? rx.CreatedBy, rx.DoctorName),
                rx.ModifiedAtUtc ?? rx.CreatedAtUtc,
                $"Prescription {rx.MedicationName} ({rx.Dosage}) Status: {rx.Status}"
            ));
        }

        // 3. Audit Lab Orders
        var labOrders = await _context.LabOrders.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var lab in labOrders)
        {
            logs.Add(new AuditLogEntryDto(
                Guid.NewGuid(),
                "LabOrder",
                lab.Id.Value.ToString(),
                lab.Status.ToString() == "Completed" ? "Result Recorded" : "Created",
                ResolveUserName(lab.ModifiedBy ?? lab.CreatedBy, lab.OrderingDoctorName),
                lab.ModifiedAtUtc ?? lab.CreatedAtUtc,
                $"Diagnostic Test {lab.TestName} Priority: {lab.Priority} Status: {lab.Status}"
            ));
        }

        // 4. Audit Invoices
        var invoices = await _context.Invoices.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var inv in invoices)
        {
            logs.Add(new AuditLogEntryDto(
                Guid.NewGuid(),
                "Invoice",
                inv.Id.Value.ToString(),
                inv.PaidAmount > 0 ? "Payment Recorded" : "Created",
                ResolveUserName(inv.ModifiedBy ?? inv.CreatedBy, "Billing Dept"),
                inv.ModifiedAtUtc ?? inv.CreatedAtUtc,
                $"Invoice #{inv.InvoiceNumber} Total: ${inv.TotalAmount} Status: {inv.Status}"
            ));
        }

        // 5. Audit Clinical Notes
        var notes = await _context.ClinicalNotes.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var n in notes)
        {
            logs.Add(new AuditLogEntryDto(
                Guid.NewGuid(),
                "ClinicalNote",
                n.Id.Value.ToString(),
                "Created",
                ResolveUserName(n.CreatedBy, n.DoctorName),
                n.CreatedAtUtc,
                $"Encounter Note Diagnosis: {n.Diagnosis} by Dr. {n.DoctorName}"
            ));
        }

        var filtered = logs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            var entityStr = request.EntityName.Trim().ToLower();
            filtered = filtered.Where(l => l.EntityName.ToLower() == entityStr);
        }

        return filtered
            .OrderByDescending(l => l.TimestampUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();
    }
}

