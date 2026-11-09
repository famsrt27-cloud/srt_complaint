using System.Globalization;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class ApiKeyExpirationPolicyTests
{
    [Fact]
    public void TryParseInputDate_WithThaiCulture_UsesGregorianHtmlDate()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            CultureInfo.CurrentUICulture = new CultureInfo("th-TH");

            var parsed = ApiKeyExpirationPolicy.TryParseInputDate("2026-11-10", out var expiresAtUtc);

            Assert.True(parsed);
            Assert.Equal(2026, expiresAtUtc.Year);
            Assert.Equal(new DateTime(2026, 11, 10, 16, 59, 59, 999, DateTimeKind.Utc).AddTicks(9999), expiresAtUtc);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void GetInputDate_WithThaiCulture_StillFormatsGregorianYear()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var nowUtc = new DateTime(2026, 11, 9, 5, 0, 0, DateTimeKind.Utc);

            var result = ApiKeyExpirationPolicy.GetInputDate(1, nowUtc);

            Assert.Equal("2026-11-10", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void IsWithinAllowedRange_AcceptsTomorrowAndDay366ByThailandCalendarDate()
    {
        var nowUtc = new DateTime(2026, 11, 9, 5, 0, 0, DateTimeKind.Utc);
        Assert.True(ApiKeyExpirationPolicy.TryParseInputDate("2026-11-10", out var tomorrow));
        Assert.True(ApiKeyExpirationPolicy.TryParseInputDate("2027-11-10", out var day366));

        Assert.True(ApiKeyExpirationPolicy.IsWithinAllowedRange(tomorrow, nowUtc));
        Assert.True(ApiKeyExpirationPolicy.IsWithinAllowedRange(day366, nowUtc));
    }

    [Fact]
    public void IsWithinAllowedRange_RejectsTodayAndDay367()
    {
        var nowUtc = new DateTime(2026, 11, 9, 5, 0, 0, DateTimeKind.Utc);
        Assert.True(ApiKeyExpirationPolicy.TryParseInputDate("2026-11-09", out var today));
        Assert.True(ApiKeyExpirationPolicy.TryParseInputDate("2027-11-11", out var day367));

        Assert.False(ApiKeyExpirationPolicy.IsWithinAllowedRange(today, nowUtc));
        Assert.False(ApiKeyExpirationPolicy.IsWithinAllowedRange(day367, nowUtc));
    }
}
