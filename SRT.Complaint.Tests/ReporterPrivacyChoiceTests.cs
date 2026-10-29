using System.ComponentModel.DataAnnotations;
using SRT.Complaint.Models;
using SRT.Complaint.Pages.Public;

namespace SRT.Complaint.Tests;

public class ReporterPrivacyChoiceTests
{
    [Theory]
    [InlineData(ReporterPrivacyChoices.Disclose, false)]
    [InlineData(ReporterPrivacyChoices.Conceal, true)]
    [InlineData(null, true)]
    [InlineData("Unexpected", true)]
    public void ShouldMask_UsesPrivacySafeFallback(string? choice, bool expected)
    {
        Assert.Equal(expected, ReporterPrivacyChoices.ShouldMask(choice));
    }

    [Theory]
    [InlineData(ReporterPrivacyChoices.Disclose, true)]
    [InlineData(ReporterPrivacyChoices.Conceal, true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Unexpected", false)]
    public void SubmitViewModel_RequiresKnownPrivacyChoice(string? choice, bool expectedValid)
    {
        var model = new ComplaintSubmitViewModel
        {
            ReporterName = "ผู้ร้องเรียน ทดสอบ",
            ReporterPhone = "0812345678",
            CategoryId = 1,
            Description = "รายละเอียดเรื่องร้องเรียนสำหรับทดสอบระบบ",
            ReporterPrivacyChoice = choice
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        var privacyChoiceValid = results.All(x =>
            x.MemberNames.All(name => name != nameof(ComplaintSubmitViewModel.ReporterPrivacyChoice)));

        Assert.Equal(expectedValid, privacyChoiceValid);
    }
}
