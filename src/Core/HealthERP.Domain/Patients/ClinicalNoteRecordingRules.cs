using System.Text.RegularExpressions;

namespace HealthERP.Domain.Patients;

public static class ClinicalNoteRecordingRules
{
    public static IReadOnlyList<string> Errors(string? complaint, string? diagnosis, string? notes, string? bp,
        int? pulse, decimal? temperature, decimal? weight)
    {
        var errors = new List<string>();
        if (new[] { complaint, diagnosis, notes, bp }.All(string.IsNullOrWhiteSpace) && pulse is null && temperature is null && weight is null)
            errors.Add("Enter at least one clinical note or vital value.");
        if (complaint?.Trim().Length > 500) errors.Add("Chief complaint must not exceed 500 characters.");
        if (diagnosis?.Trim().Length > 300) errors.Add("Diagnosis must not exceed 300 characters.");
        if (notes?.Trim().Length > 2000) errors.Add("Notes must not exceed 2000 characters.");
        if (!string.IsNullOrWhiteSpace(bp))
        {
            var match = Regex.Match(bp.Trim(), @"\A([0-9]{2,3})/([0-9]{2,3})\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var systolic) || !int.TryParse(match.Groups[2].Value, out var diastolic)
                || systolic is < 40 or > 300 || diastolic is < 20 or > 200 || systolic <= diastolic)
                errors.Add("Blood pressure must be systolic/diastolic: systolic 40–300, diastolic 20–200, with systolic higher.");
        }
        if (pulse is < 1 or > 300) errors.Add("Heart rate must be between 1 and 300 bpm.");
        if (temperature is < 25 or > 45) errors.Add("Temperature must be between 25 and 45 °C.");
        if (weight is <= 0 or > 500) errors.Add("Weight must be greater than 0 and no more than 500 kg.");
        if ((temperature.HasValue && decimal.Round(temperature.Value, 2) != temperature) || (weight.HasValue && decimal.Round(weight.Value, 2) != weight))
            errors.Add("Temperature and weight support at most two decimal places.");
        return errors;
    }
}
