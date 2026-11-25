using Microsoft.AspNetCore.HttpOverrides;
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestHeadersTotalSize = 64 * 1024);



builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>().Configure<IConfiguration>((options, configuration) =>
{
    var companyDomain = configuration["Cors:CompanyDomain"]?.Trim().TrimStart('.').ToLowerInvariant();
    var extraOrigins = (configuration.GetSection("Cors:ExtraOrigins").Get<string[]>() ?? [])
        .Select(o => o.Trim().TrimEnd('/'))
        .Where(o => Uri.TryCreate(o, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http") && uri.AbsolutePath == "/")
        .ToArray();

    var staticOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
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
        "http://localhost:5175",
        "https://localhost:5171",
        "https://localhost:5172",
        "https://localhost:5173",
        "https://localhost:5174",
        "https://localhost:5175"
    };
    foreach (var o in extraOrigins) staticOrigins.Add(o);

    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (staticOrigins.Contains(origin.TrimEnd('/')))
                return true;

            if (!string.IsNullOrWhiteSpace(companyDomain) && Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                var host = uri.Host.ToLowerInvariant();
                if (host == companyDomain || host.EndsWith("." + companyDomain))
                    return true;
            }

            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options => {
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach(var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(System.Net.IPAddress.Parse(address));
});
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.UseForwardedHeaders();
app.UseCors();

app.MapGet("/", () => "AIWorkHub API Gateway");

app.MapReverseProxy();

app.Run();

public partial class Program
{
}
