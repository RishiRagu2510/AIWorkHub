using IdentityTenant.Api.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Authorization.Tests;

public sealed class SessionOriginTests
{
    [Theory]
    [InlineData("https://landing-85fe8.web.app", true)]          
    [InlineData("https://landing-85fe8.firebaseapp.com", true)]
    [InlineData("https://landing-85fe8.web.app.evil.test", false)]
    [InlineData("http://localhost:5171", true)]    
    [InlineData("http://localhost:5172", true)]
    [InlineData("http://localhost:5176", false)]
    [InlineData("http://localhost:51710", false)]
    [InlineData("https://localhost:5171", false)]
    public void TheLandingSiteMaySignIn_AndNeighbouringAddressesMayNot(string origin, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Scheme = "http";
        http.Request.Host = new HostString("localhost", 5204);
        http.Request.Headers.Origin = origin;
        var action = new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        new RequireSessionOriginAttribute().OnActionExecuting(action);
        if (allowed) Assert.Null(action.Result);
        else Assert.Equal(403, Assert.IsType<StatusCodeResult>(action.Result).StatusCode);
    }

    [Fact]
    public void ADeployedLandingSite_IsAllowedWhenItsAddressIsConfigured()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Frontend:PortalOrigins:0"] = "https://start.example.com/" }).Build());
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("api.example.com");
        http.Request.Headers.Origin = "https://start.example.com";
        var action = new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        new RequireSessionOriginAttribute().OnActionExecuting(action);
        Assert.Null(action.Result);
    }

    [Theory]
    [InlineData("https://app.example.com", true)]
    [InlineData("https://api.example.com", true)]
    [InlineData("https://evil.example.com", false)]
    [InlineData("https://app.example.com.evil.test", false)]
    [InlineData("null", false)]
    [InlineData("", false)]
    public void CookieMutationRequiresTrustedOrigin(string origin, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://app.example.com" }).Build());
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("api.example.com");
        http.Request.Headers.Origin = origin;
        var action = new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        new RequireSessionOriginAttribute().OnActionExecuting(action);
        if (allowed) Assert.Null(action.Result);
        else Assert.Equal(403, Assert.IsType<StatusCodeResult>(action.Result).StatusCode);
    }
}
