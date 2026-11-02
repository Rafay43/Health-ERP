namespace HealthERP.Application.Common.Configurations;

/// <summary>Authoritative facility calendar; independent of browser and server local time.</summary>
public sealed class FacilityTimeZone(string timeZoneId)
{
    public TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

    public DateOnly GetDate(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    public (DateTime StartUtc, DateTime EndUtc) GetUtcWindow(DateOnly date)
    {
        if (date == DateOnly.MaxValue) throw new ArgumentException("Date must be earlier than 9999-12-31.");
        return (Boundary(date), Boundary(date.AddDays(1)));
    }

    private DateTime Boundary(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // Rare midnight transitions require an explicit rule; never silently pick an offset.
        if (Zone.IsInvalidTime(local) || Zone.IsAmbiguousTime(local))
            throw new ArgumentException("The requested date has an unsupported facility midnight timezone transition.");
        try { return TimeZoneInfo.ConvertTimeToUtc(local, Zone); }
        catch (ArgumentException) { throw new ArgumentException("Date is outside the supported facility calendar range."); }
    }
}
