using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SRT.Complaint.Models;

namespace SRT.Complaint.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireScopeAttribute(string scope) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var apiKey = context.HttpContext.Items["ApiKey"] as ApiKey;
        if (apiKey == null || !apiKey.Scopes.Any(s => string.Equals(s.Scope, scope, StringComparison.Ordinal)))
        {
            context.Result = new ObjectResult(
                new { error = new { code = "insufficient_scope", message = $"Required scope: {scope}", traceId = context.HttpContext.TraceIdentifier } }) { StatusCode = 403 };
        }
    }
}
