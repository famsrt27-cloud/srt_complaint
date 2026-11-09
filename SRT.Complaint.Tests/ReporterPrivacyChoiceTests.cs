using System.ComponentModel.DataAnnotations;
using SRT.Complaint.Models;
using SRT.Complaint.Pages.Public;

namespace SRT.Complaint.Tests;

public class ReporterPrivacyChoiceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void SubmitViewModel_RequiresExplicitTermsAcceptance(bool accepted, bool expectedValid)
    {
        var model = new ComplaintSubmitViewModel
        {
            AcceptedTerms = accepted,
            TermsVersion = "2026-10-30T00:00:00.0000000Z"
        };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        var acceptanceValid = results.All(x =>
            x.MemberNames.All(name => name != nameof(ComplaintSubmitViewModel.AcceptedTerms)));
        Assert.Equal(expectedValid, acceptanceValid);
    }

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

    [Fact]
    public void SubmitViewModel_RequiresStationAndIncidentDate()
    {
        var model = new ComplaintSubmitViewModel
        {
            ReporterName = "ผู้ร้องเรียน ทดสอบ",
            ReporterPhone = "0812345678",
            ReporterPrivacyChoice = ReporterPrivacyChoices.Disclose,
            SubmissionPurpose = ComplaintSubmissionPurposes.ServiceImprovement,
            CategoryId = 1,
            Description = "รายละเอียดเรื่องร้องเรียนสำหรับทดสอบระบบ"
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(ComplaintSubmitViewModel.SubjectStation)) &&
            result.ErrorMessage == "กรุณากรอกสถานี / จุดที่เกิดเหตุ");
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(ComplaintSubmitViewModel.IncidentDate)) &&
            result.ErrorMessage == "กรุณาระบุวันที่เกิดเหตุ");
    }

    [Fact]
    public void SubmitViewModel_AcceptsStationAndIncidentDate()
    {
        var model = new ComplaintSubmitViewModel
        {
            SubjectStation = "สถานีกรุงเทพ",
            IncidentDate = new DateTime(2026, 11, 3)
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.DoesNotContain(results, result => result.MemberNames.Any(name =>
            name is nameof(ComplaintSubmitViewModel.SubjectStation) or nameof(ComplaintSubmitViewModel.IncidentDate)));
    }
}
