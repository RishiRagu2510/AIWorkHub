using IdentityTenant.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityTenant.Api.Authentication;

public static class CompanyGoogleTicket
{
    public static async Task HandleAsync(TicketReceivedContext context)
    {
        var service = context.HttpContext.RequestServices.GetRequiredService<CompanyPrincipalService>();
        try
        {
            if (context.Principal is null)
                throw new LoginRejectedException("INVALID_GOOGLE_IDENTITY", "Google identity could not be verified.");
            context.Principal = await service.SignInGoogleAsync(context.Principal, context.HttpContext.RequestAborted);
        }
        catch (LoginRejectedException exception)
        {
            context.HandleResponse();
            if (!string.IsNullOrWhiteSpace(context.ReturnUri))
            {
                context.Response.Redirect(QueryHelpers.AddQueryString(context.ReturnUri, "error", exception.Code));
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { code = exception.Code, message = exception.Message },
                context.HttpContext.RequestAborted);
        }
    }
}