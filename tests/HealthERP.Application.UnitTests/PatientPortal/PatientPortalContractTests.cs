using System.Text.Json;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientPortal.Queries.GetMyHealthRecord;
using HealthERP.Application.PatientPortal.Queries.GetMyPrescriptions;
using HealthERP.Application.PatientPortal.Queries.GetMyLabResults;
using HealthERP.Application.PatientPortal.Queries.GetMyBilling;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Pharmacy;
using HealthERP.Domain.Laboratory;
using HealthERP.Domain.Billing;
using MockQueryable.Moq;
using Moq;

namespace HealthERP.Application.UnitTests.PatientPortal;

public class PatientPortalContractTests
{
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<ICurrentPatientService> _currentPatient = new();
    private readonly Patient _patient = Patient.Register("MRN-0009", "Portal", "Patient", new DateOnly(1990, 1, 1));
    private readonly Patient _otherPatient = Patient.Register("MRN-0010", "Other", "Patient", new DateOnly(1991, 1, 1));

    public PatientPortalContractTests()
    {
        _patient.LinkUserId(Guid.NewGuid());
        _otherPatient.LinkUserId(Guid.NewGuid());
        _currentPatient.Setup(s => s.GetCurrentPatientAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
    }

    [Fact]
    public async Task HealthRecord_ProjectsVitalsAndSafeEncounters_OnlyForLinkedPatient()
    {
        const string internalNotes = "INTERNAL_PROVIDER_NOTES_MUST_NOT_BE_EXPOSED";
        var ownNote = ClinicalNote.Create(_patient.Id, "Dr. Portal", "Annual checkup", "Routine examination",
            internalNotes, "120/80", 72, 37m, 70m);
        var otherNote = ClinicalNote.Create(_otherPatient.Id, "Dr. Other", "Other complaint", "Other diagnosis",
            "Other patient's private notes", "160/100", 110, 39m, 90m);
        _context.Setup(c => c.ClinicalNotes).Returns(new List<ClinicalNote> { ownNote, otherNote }.BuildMockDbSet().Object);

        var result = await new GetMyHealthRecordQueryHandler(_context.Object, _currentPatient.Object)
            .Handle(new GetMyHealthRecordQuery(), CancellationToken.None);

        Assert.NotNull(result.LatestVitals);
        Assert.Equal("120/80", result.LatestVitals.BloodPressure);
        Assert.Equal(72, result.LatestVitals.HeartRateBpm);
        Assert.Equal(37m, result.LatestVitals.TemperatureCelsius);
        Assert.Equal(70m, result.LatestVitals.WeightKg);
        Assert.Equal(ownNote.NoteDateUtc, result.LatestVitals.RecordedDateUtc);
        var encounter = Assert.Single(result.Encounters);
        Assert.Equal(ownNote.Id.Value, encounter.Id);
        Assert.Equal("Dr. Portal", encounter.DoctorName);
        Assert.Equal("Annual checkup", encounter.ChiefComplaint);
        Assert.Equal("Routine examination", encounter.Diagnosis);
        Assert.Equal(ownNote.NoteDateUtc, encounter.NoteDateUtc);

        var jsonText = JsonSerializer.Serialize(result, JsonSerializerOptions.Web);
        using var json = JsonDocument.Parse(jsonText);
        Assert.Equal("120/80", json.RootElement.GetProperty("latestVitals").GetProperty("bloodPressure").GetString());
        Assert.Equal(72, json.RootElement.GetProperty("latestVitals").GetProperty("heartRateBpm").GetInt32());
        Assert.True(json.RootElement.GetProperty("latestVitals").TryGetProperty("recordedDateUtc", out _));
        var serializedEncounter = json.RootElement.GetProperty("encounters")[0];
        Assert.True(serializedEncounter.TryGetProperty("noteDateUtc", out _));
        Assert.False(serializedEncounter.TryGetProperty("notes", out _));
        Assert.DoesNotContain(internalNotes, jsonText);
        Assert.DoesNotContain("Other complaint", jsonText);
    }

    [Fact]
    public async Task HealthRecord_WithoutOwnVitals_DoesNotUseAnotherPatientsVitals()
    {
        var ownNote = ClinicalNote.Create(_patient.Id, "Dr. Portal", "Checkup", "Routine", "Internal notes");
        var otherNote = ClinicalNote.Create(_otherPatient.Id, "Dr. Other", "Other", "Other", "Internal", "120/80", 72);
        _context.Setup(c => c.ClinicalNotes).Returns(new List<ClinicalNote> { ownNote, otherNote }.BuildMockDbSet().Object);

        var result = await new GetMyHealthRecordQueryHandler(_context.Object, _currentPatient.Object)
            .Handle(new GetMyHealthRecordQuery(), CancellationToken.None);

        Assert.Null(result.LatestVitals);
        Assert.Equal(ownNote.Id.Value, Assert.Single(result.Encounters).Id);
    }

    [Fact]
    public async Task Prescriptions_ExposeDoctorAndDuration_OnlyForLinkedPatient()
    {
        var own = Prescription.Create(_patient.Id, null, "Dr. Portal", "Atorvastatin 20mg", "500mg", "Twice daily", 7, 0, "After meals");
        var other = Prescription.Create(_otherPatient.Id, null, "Dr. Other", "Other medication", "10mg", "Daily", 30, 1, "Private instructions");
        _context.Setup(c => c.Prescriptions).Returns(new List<Prescription> { own, other }.BuildMockDbSet().Object);

        var result = await new GetMyPrescriptionsQueryHandler(_context.Object, _currentPatient.Object)
            .Handle(new GetMyPrescriptionsQuery(), CancellationToken.None);

        var prescription = Assert.Single(result);
        Assert.Equal(own.Id.Value, prescription.Id);
        Assert.Equal("Dr. Portal", prescription.DoctorName);
        Assert.Equal(7, prescription.DurationDays);
        Assert.Equal("500mg", prescription.Dosage);
        Assert.Equal("Twice daily", prescription.Frequency);
        Assert.Equal("After meals", prescription.Instructions);
        Assert.Equal("Pending", prescription.Status);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonSerializerOptions.Web));
        Assert.Equal("Dr. Portal", json.RootElement[0].GetProperty("doctorName").GetString());
        Assert.Equal(7, json.RootElement[0].GetProperty("durationDays").GetInt32());
    }

    [Fact]
    public async Task UnlinkedAccount_CannotReadHealthRecordOrPrescriptions()
    {
        _currentPatient.Setup(s => s.GetCurrentPatientAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Patient?)null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetMyHealthRecordQueryHandler(_context.Object, _currentPatient.Object).Handle(new GetMyHealthRecordQuery(), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetMyPrescriptionsQueryHandler(_context.Object, _currentPatient.Object).Handle(new GetMyPrescriptionsQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task LabResults_ExcludePendingCancelledAndAnotherPatientsCompletedOrders()
    {
        var ownCompleted = LabOrder.Create(_patient.Id, null, "Dr. Portal", "CBC", "CBC");
        ownCompleted.RecordResult("normal", null, "Technician");
        var ownPending = LabOrder.Create(_patient.Id, null, "Dr. Portal", "Pending", "PENDING");
        var ownCancelled = LabOrder.Create(_patient.Id, null, "Dr. Portal", "Cancelled", "CANCELLED");
        ownCancelled.Cancel();
        var otherCompleted = LabOrder.Create(_otherPatient.Id, null, "Dr. Other", "Other result", "OTHER");
        otherCompleted.RecordResult("private result", null, "Technician");
        _context.Setup(c => c.LabOrders).Returns(new List<LabOrder>
            { ownCompleted, ownPending, ownCancelled, otherCompleted }.BuildMockDbSet().Object);

        var result = await new GetMyLabResultsQueryHandler(_context.Object, _currentPatient.Object)
            .Handle(new GetMyLabResultsQuery(CompletedOnly: true), CancellationToken.None);

        var lab = Assert.Single(result);
        Assert.Equal(ownCompleted.Id.Value, lab.Id);
        Assert.Equal("Completed", lab.Status);
        Assert.Equal("normal", lab.ResultValue);
    }

    [Fact]
    public async Task Billing_SerializesLineTotalAsTotalPrice_AndPreservesBalanceAndOwnership()
    {
        var ownInvoice = Invoice.Create(_patient.Id, "INV-OWN", DateTime.UtcNow.AddDays(30));
        ownInvoice.AddLineItem("Medical Consultation", 1, 150m);
        var otherInvoice = Invoice.Create(_otherPatient.Id, "INV-OTHER", DateTime.UtcNow.AddDays(30));
        otherInvoice.AddLineItem("Private billing", 1, 300m);
        _context.Setup(c => c.Invoices).Returns(new List<Invoice> { ownInvoice, otherInvoice }.BuildMockDbSet().Object);

        var result = await new GetMyBillingQueryHandler(_context.Object, _currentPatient.Object)
            .Handle(new GetMyBillingQuery(), CancellationToken.None);

        var invoice = Assert.Single(result);
        Assert.Equal(150m, invoice.BalanceDue);
        Assert.Equal("Pending", invoice.Status);
        Assert.Equal(150m, Assert.Single(invoice.LineItems).TotalPrice);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonSerializerOptions.Web));
        Assert.Equal(150m, json.RootElement[0].GetProperty("lineItems")[0].GetProperty("totalPrice").GetDecimal());
    }
}
