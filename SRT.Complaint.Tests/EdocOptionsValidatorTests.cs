using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class EdocOptionsValidatorTests
{
    private readonly EdocOptionsValidator _validator = new();

    [Fact]
    public void Validate_AllowsDisabledConfiguration()
    {
        var result = _validator.Validate(null, new EdocOptions { Enabled = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_AcceptsConfiguredBasketListAndCategoryMapping()
    {
        var options = ValidOptions();
        options.Baskets.Add(new EdocBasketOption { Id = "1234567890123", Name = "ฝ่ายทดสอบ" });
        options.CategoryBasketMappings["7"] = "1234567890123";

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_RejectsUnknownMappedBasket()
    {
        var options = ValidOptions();
        options.Baskets.Add(new EdocBasketOption { Id = "1234567890123", Name = "ฝ่ายทดสอบ" });
        options.CategoryBasketMappings["7"] = "9999999999999";

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, x => x.Contains("CategoryBasketMappings", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDevelopmentUrlForProduction()
    {
        var options = ValidOptions();
        options.Environment = "Production";
        options.BasketId = "1234567890123";

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, x => x.Contains("ApiDev", StringComparison.Ordinal));
    }

    private static EdocOptions ValidOptions() => new()
    {
        Enabled = true,
        BaseUrl = "https://edoc.railway.co.th/ApiDev/RwSarabanAPI/",
        AllowedHosts = ["edoc.railway.co.th"],
        Username = "service-user",
        Password = "secret",
        SarabanUsername = "saraban-user",
        AppId = "1",
        FromName = "งานรับเรื่องร้องเรียน",
        DefaultSendTo = "หน่วยงานทดสอบ"
    };
}
