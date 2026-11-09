using System.Globalization;

namespace SRT.Complaint.Services;

public static class ApiKeyExpirationPolicy
{
    private const string HtmlDateFormat = "yyyy-MM-dd";
    private static readonly TimeZoneInfo ThailandTimeZone = ResolveThailandTimeZone();

    public static string GetInputDate(int daysFromToday, DateTime? utcNow = null)
    {
        var today = GetThailandDate(utcNow ?? DateTime.UtcNow);
        return today.AddDays(daysFromToday).ToString(HtmlDateFormat, CultureInfo.InvariantCulture);
    }

    public static bool TryParseInputDate(string? value, out DateTime expiresAtUtc)
    {
        expiresAtUtc = default;
        if (!DateOnly.TryParseExact(
                value,
                HtmlDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return false;
        }

        var endOfDayInThailand = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Unspecified);
        expiresAtUtc = TimeZoneInfo.ConvertTimeToUtc(endOfDayInThailand, ThailandTimeZone);
        return true;
    }

    public static bool IsWithinAllowedRange(DateTime? expiresAtUtc, DateTime? utcNow = null)
    {
        if (!expiresAtUtc.HasValue)
            return false;

        var now = EnsureUtc(utcNow ?? DateTime.UtcNow);
        var expiresAt = EnsureUtc(expiresAtUtc.Value);
        if (expiresAt <= now)
            return false;

        var today = GetThailandDate(now);
        var expirationDate = GetThailandDate(expiresAt);
        var daysFromToday = expirationDate.DayNumber - today.DayNumber;
        return daysFromToday is >= 1 and <= 366;
    }

    private static DateOnly GetThailandDate(DateTime utcDateTime)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(utcDateTime), ThailandTimeZone));

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static TimeZoneInfo ResolveThailandTimeZone()
    {
        foreach (var id in new[] { "Asia/Bangkok", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Asia/Bangkok",
            TimeSpan.FromHours(7),
            "Thailand Standard Time",
            "Thailand Standard Time");
    }
}
