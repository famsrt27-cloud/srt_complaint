using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace SRT.Complaint.OpenApi;

public sealed class ApiOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "SRT Complaint Partner API",
            Version = "v1",
            Description = "API สำหรับเชื่อมต่อระบบรับเรื่องร้องเรียนแบบสองทาง แยกข้อมูลตาม Partner"
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = "X-API-Key",
            In = ParameterLocation.Header,
            Description = "API Key ที่ออกให้แต่ละ Partner และ Environment"
        };

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            }] = Array.Empty<string>()
        };

        foreach (var path in document.Paths.Values)
        foreach (var operation in path.Operations.Values)
        {
            operation.Security ??= [];
            operation.Security.Add(requirement);
        }

        return Task.CompletedTask;
    }
}
