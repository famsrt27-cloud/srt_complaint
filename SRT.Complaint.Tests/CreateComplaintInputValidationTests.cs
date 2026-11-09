using System.ComponentModel.DataAnnotations;
using SRT.Complaint.Pages.Staff;

namespace SRT.Complaint.Tests;

public class CreateComplaintInputValidationTests
{
    [Fact]
    public void Validate_RequiresStationAndIncidentDate()
    {
        var input = ValidInput();
        input.SubjectStation = string.Empty;
        input.IncidentDate = null;

        var errors = Validate(input);

        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(input.SubjectStation)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(input.IncidentDate)));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("08123456789")]
    [InlineData("not-a-phone")]
    public void Validate_RejectsInvalidPhone(string phone)
    {
        var input = ValidInput();
        input.ReporterPhone = phone;

        Assert.Contains(Validate(input), x => x.MemberNames.Contains(nameof(input.ReporterPhone)));
    }

    [Fact]
    public void Validate_NormalizesFormattedPhone()
    {
        var input = ValidInput();
        input.ReporterPhone = "081-234-5678";

        Assert.Empty(Validate(input));
        Assert.Equal("0812345678", input.ReporterPhone);
    }

    [Fact]
    public void Validate_RejectsUnknownChannelAndShortDescription()
    {
        var input = ValidInput();
        input.Channel = "Unknown";
        input.Description = "สั้นเกินไป";

        var errors = Validate(input);

        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(input.Channel)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(input.Description)));
    }

    private static CreateComplaintInput ValidInput() => new()
    {
        Channel = "Phone",
        ReporterName = "ผู้ร้องเรียน ทดสอบ",
        ReporterPhone = "0812345678",
        CategoryId = 1,
        SubjectStation = "สถานีกรุงเทพ",
        IncidentDate = new DateTime(2026, 11, 4),
        Description = "รายละเอียดเหตุการณ์สำหรับใช้ทดสอบการสร้างเรื่องร้องเรียน"
    };

    private static List<ValidationResult> Validate(CreateComplaintInput input)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);
        return results;
    }
}
