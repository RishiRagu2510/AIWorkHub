using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IdentityTenant.Api.Authentication;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireSessionOriginAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        var origin = request.Headers.Origin.ToString();
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var frontend = configuration["Frontend:BaseUrl"];
        var sameOrigin = $"{request.Scheme}://{request.Host}";
        var frontendOrigin = Uri.TryCreate(frontend, UriKind.Absolute, out var uri) &&
                             (uri.Scheme == "https" || uri.Scheme == "http")
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;
        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            sameOrigin,
            "https://gridhub-85fe8.web.app",
            "https://gridhub-85fe8.firebaseapp.com",
            "https://hub-85fe8.web.app",
            "https://hub-85fe8.firebaseapp.com",
            "https://chronos-85fe8.web.app",
            "https://chronos-85fe8.firebaseapp.com",
            "https://persona-85fe8.web.app",
            "https://persona-85fe8.firebaseapp.com",
            "https://landing-85fe8.web.app",
            "https://landing-85fe8.firebaseapp.com",
            "http://localhost:5171",
            "http://localhost:5172",
            "http://localhost:5173",
            "http://localhost:5174",
            "http://localhost:5175"
        };
        if (!string.IsNullOrEmpty(frontendOrigin)) allowedOrigins.Add(frontendOrigin);
        foreach (var portal in configuration.GetSection("Frontend:PortalOrigins").Get<string[]>() ?? [])
            allowedOrigins.Add(portal.TrimEnd('/'));

        if (string.IsNullOrWhiteSpace(origin) || !allowedOrigins.Contains(origin.TrimEnd('/')))
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
    }
}