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
            Description = "Partner API สำหรับรับเรื่องร้องเรียน ติดตามสถานะ สถิติ และ signed webhooks โดยแยกข้อมูลตาม Partner"
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = "X-API-Key",
            In = ParameterLocation.Header,
            Description = "API key ที่ผูกกับ Partner, environment, scopes และ data-access mode"
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
            operation.Responses.TryAdd("401", ErrorResponse("API key is missing, invalid, expired, revoked, or belongs to another environment."));
            operation.Responses.TryAdd("403", ErrorResponse("The API key does not have the required scope, data access, or allowed source IP."));
            operation.Responses.TryAdd("429", RateLimitResponse());
            operation.Responses.TryAdd("503", ErrorResponse("Distributed rate limiter is temporarily unavailable. Retry writes after Retry-After."));
            operation.Responses.TryAdd("500", ErrorResponse("Unexpected server error. Use error.traceId when contacting support."));

            if (operation.OperationId is "GetComplaintV1" or "GetComplaintByExternalReferenceV1" or "GetComplaintStatusV1")
                AddEtagHeader(operation);

            if (operation.OperationId == "UpdateComplaintStatusV1")
            {
                var ifMatch = operation.Parameters?.FirstOrDefault(parameter =>
                    parameter.Name.Equals("If-Match", StringComparison.OrdinalIgnoreCase));
                if (ifMatch != null)
                {
                    ifMatch.Required = true;
                    ifMatch.Description = "ETag/version from the latest complaint read response.";
                }

                operation.Description = "Internal API keys only. Enforces workflow transition rules and optimistic concurrency.";
                operation.Responses.TryAdd("409", ErrorResponse("Status transition is not allowed for the current workflow step."));
                operation.Responses.TryAdd("412", ErrorResponse("If-Match does not match the current complaint version."));
                operation.Responses.TryAdd("428", ErrorResponse("If-Match is required."));
                AddEtagHeader(operation);
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiResponse ErrorResponse(string description) => new()
    {
        Description = description
    };

    private static OpenApiResponse RateLimitResponse() => new()
    {
        Description = "Per-API-key fixed-window rate limit exceeded.",
        Headers = new Dictionary<string, OpenApiHeader>
        {
            ["Retry-After"] = new() { Description = "Seconds until the current window resets." },
            ["X-RateLimit-Limit"] = new() { Description = "Configured requests per minute." },
            ["X-RateLimit-Remaining"] = new() { Description = "Requests remaining in the current window." },
            ["X-RateLimit-Reset"] = new() { Description = "Window reset time as Unix epoch seconds." }
        }
    };

    private static void AddEtagHeader(OpenApiOperation operation)
    {
        if (!operation.Responses.TryGetValue("200", out var response))
            return;

        response.Headers ??= new Dictionary<string, OpenApiHeader>();
        response.Headers.TryAdd("ETag", new OpenApiHeader
        {
            Description = "Quoted Base64 SQL Server rowversion. Send this value in If-Match for status updates."
        });
    }
}
