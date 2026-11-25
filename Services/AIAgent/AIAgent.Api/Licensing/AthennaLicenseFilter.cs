using AIAgent.Application.Interfaces;
using Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AIAgent.Api.Licensing;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWithoutAthennaLicenseAttribute : Attribute;

public sealed class AthennaLicenseFilter(ICallerIdentityProvider identity) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var skip = context.ActionDescriptor.EndpointMetadata.OfType<AllowWithoutAthennaLicenseAttribute>().Any();
        if (!skip && identity.HasCredentials)
        {
            var caller = await identity.VerifyAsync(context.HttpContext.RequestAborted);
            if (caller is not null && !FeatureLicense.IsLicensed(caller.AllowedFeatures, caller.AllowedPortals, FeatureLicense.Athenna))
            {
                context.Result = new ObjectResult(new
                {
                    error = "The Athenna AI chat is not included in your company's plan. It comes with AIWorkHub.",
                    code = "FEATURE_NOT_LICENSED",
                    feature = FeatureLicense.Athenna,
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }
        }
        await next();
    }
}
