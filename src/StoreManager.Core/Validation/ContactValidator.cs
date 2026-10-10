using System.Text.RegularExpressions;
using StoreManager.Core.Entities;
using StoreManager.Core.Formatting;

namespace StoreManager.Core.Validation;

public sealed record ValidationError(string Field, string Code);

/// <summary>Pure validation, so the UI can show inline messages and tests can cover every rule.</summary>
public static partial class ContactValidator
{
    public const string Required = "Required";
    public const string Invalid = "Invalid";

    [GeneratedRegex(@"^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")]
    private static partial Regex GstinPattern();

    /// <summary>Normalises phone fields and GSTIN in place and returns any errors.</summary>
    public static List<ValidationError> ValidateAndNormalize(Contact c)
    {
        var errors = new List<ValidationError>();

        c.Name = (c.Name ?? "").Trim();
        if (c.Name.Length == 0) errors.Add(new(nameof(Contact.Name), Required));

        if (string.IsNullOrWhiteSpace(c.Mobile)) errors.Add(new(nameof(Contact.Mobile), Required));
        else if (PhoneNumber.TryNormalize(c.Mobile, out var m)) c.Mobile = m;
        else errors.Add(new(nameof(Contact.Mobile), Invalid));

        c.WhatsApp = NormalizeOptionalPhone(c.WhatsApp, nameof(Contact.WhatsApp), errors);
        c.AltPhone = NormalizeOptionalPhone(c.AltPhone, nameof(Contact.AltPhone), errors);

        c.Gstin = string.IsNullOrWhiteSpace(c.Gstin) ? null : c.Gstin.Trim().ToUpperInvariant();
        if (c.Gstin is not null && !GstinPattern().IsMatch(c.Gstin))
            errors.Add(new(nameof(Contact.Gstin), Invalid));

        c.City = Clean(c.City);
        c.Address = Clean(c.Address);
        c.Notes = Clean(c.Notes);
        return errors;
    }

    private static string? NormalizeOptionalPhone(string? raw, string field, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (PhoneNumber.TryNormalize(raw, out var n)) return n;
        errors.Add(new(field, Invalid));
        return raw;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
