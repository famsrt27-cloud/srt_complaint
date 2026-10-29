namespace SRT.Complaint.Models;

public static class ReporterPrivacyChoices
{
    public const string Disclose = "Disclose";
    public const string Conceal = "Conceal";
    public const string ValidationPattern = "^(Disclose|Conceal)$";

    public static bool IsValid(string? value)
        => value is Disclose or Conceal;

    // Privacy-safe fallback for legacy, staff-created, API, or malformed records.
    public static bool ShouldMask(string? value)
        => !string.Equals(value, Disclose, StringComparison.Ordinal);
}
