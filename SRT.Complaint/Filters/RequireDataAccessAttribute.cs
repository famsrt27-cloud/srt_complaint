using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SRT.Complaint.Models;

namespace SRT.Complaint.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireDataAccessAttribute(string accessMode) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var apiKey = context.HttpContext.Items["ApiKey"] as ApiKey;
        if (apiKey == null || !string.Equals(apiKey.DataAccessMode, accessMode, StringComparison.Ordinal))
        {
            context.Result = new ObjectResult(new
            {
                error = new
                {
                    code = "insufficient_data_access",
                    message = $"Required data access: {accessMode}",
                    traceId = context.HttpContext.TraceIdentifier
                }
            }) { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
