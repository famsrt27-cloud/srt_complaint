using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SRT.Complaint.Controllers.Api;

namespace SRT.Complaint.Tests;

public class ApiDtoValidationTests
{
    [Fact]
    public void PostComplaint_InvalidLongitude_ReturnsModelErrorWithoutMetadataException()
    {
        var dto = new ComplaintCreateDto(
            ExternalReference: "PARTNER-001",
            ReporterName: "API regression test",
            ReporterPhone: "0812345678",
            ReporterEmail: "reporter@example.com",
            CategoryId: 1,
            SubCategoryId: null,
            SubjectStation: null,
            IncidentDate: null,
            Description: "Valid complaint description",
            Latitude: 13.7563,
            Longitude: 181);

        var modelState = Validate(dto);

        Assert.False(modelState.IsValid);
        var longitude = modelState[nameof(ComplaintCreateDto.Longitude)]!;
        Assert.NotNull(longitude);
        Assert.NotEmpty(longitude.Errors);
    }

    [Theory]
    [MemberData(nameof(OtherRequestDtos))]
    public void OtherPositionalRequestDtos_UseConstructorValidationWithoutMetadataException(object dto)
    {
        var modelState = Validate(dto);

        Assert.False(modelState.IsValid);
    }

    public static TheoryData<object> OtherRequestDtos => new()
    {
        new UpdateStatusDto("", new string('x', 2001)),
        new CreateWebhookDto("", "not-a-url", [])
    };

    private static ModelStateDictionary Validate(object model)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        var validator = serviceProvider.GetRequiredService<IObjectModelValidator>();

        validator.Validate(actionContext, validationState: null, prefix: string.Empty, model);
        return actionContext.ModelState;
    }
}
