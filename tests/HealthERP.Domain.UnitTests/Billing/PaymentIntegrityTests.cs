using HealthERP.Domain.Billing;
using HealthERP.Domain.Patients;

namespace HealthERP.Domain.UnitTests.Billing;

public class PaymentIntegrityTests
{
    private static Invoice Invoice()
    {
        var invoice = HealthERP.Domain.Billing.Invoice.Create(PatientId.New(), "TEST", DateTime.UtcNow.AddDays(1));
        invoice.AddLineItem("Consultation", 1, 100);
        return invoice;
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("30.005")]
    [InlineData("10000000000000000")]
    public void InvalidMoney_DoesNotMutateInvoice(string input)
    {
        var invoice = Invoice();
        Assert.Throws<ArgumentException>(() => invoice.RecordPayment(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture), PaymentMethod.Cash));
        Assert.Equal(0m, invoice.PaidAmount); Assert.Empty(invoice.Payments); Assert.Equal(InvoiceStatus.Pending, invoice.Status);
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(99, null, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, null, 501)]
    public void InvalidPaymentFields_AreRejectedBeforeMutation(int method, int? referenceLength, int? notesLength)
    {
        var invoice = Invoice();
        Assert.Throws<ArgumentException>(() => invoice.RecordPayment(30, (PaymentMethod)method,
            referenceLength.HasValue ? new string('r', referenceLength.Value) : null,
            notesLength.HasValue ? new string('n', notesLength.Value) : null));
        Assert.Equal(0m, invoice.PaidAmount); Assert.Empty(invoice.Payments);
    }

    [Fact]
    public void CancelledInvoice_CannotBeReopenedByPayment()
    {
        var invoice = Invoice(); invoice.Cancel();
        Assert.Throws<InvalidOperationException>(() => invoice.RecordPayment(30, PaymentMethod.Cash));
        Assert.Equal(InvoiceStatus.Cancelled, invoice.Status); Assert.Equal(0m, invoice.PaidAmount); Assert.Empty(invoice.Payments);
    }
}
