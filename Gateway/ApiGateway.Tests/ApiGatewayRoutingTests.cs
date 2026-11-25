using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.Tests;

public sealed class ApiGatewayRoutingTests : IAsyncLifetime
{
    private WebApplication? _backend;
    private GatewayFactory? _gatewayFactory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        var backendBuilder = WebApplication.CreateBuilder();

        backendBuilder.WebHost.ConfigureKestrel(
            options =>
            {
                options.Listen(
                    IPAddress.Loopback,
                    0);
            });

        _backend = backendBuilder.Build();

        _backend.Run(async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";

            var payload = new ForwardedRequest(
                context.Request.Path.Value,
                context.Request.QueryString.Value,
                context.Request.Headers.Authorization.ToString(),
                context.Request.Host.Value, context.Request.Headers.Cookie.ToString());

            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);

            await context.Response.Body.WriteAsync(bytes);
        });

        await _backend.StartAsync();

        var server =
            _backend.Services.GetRequiredService<IServer>();

        var addressesFeature =
            server.Features.Get<IServerAddressesFeature>();

        var backendAddress =
            addressesFeature!
                .Addresses
                .Single()
                .TrimEnd('/') + "/";

        _gatewayFactory =
            new GatewayFactory(backendAddress);

        _client =
            _gatewayFactory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _gatewayFactory?.Dispose();

        if (_backend is not null)
        {
            await _backend.StopAsync();
            await _backend.DisposeAsync();
        }
    }

    private async Task<string?> AllowedOrigin(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/me");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        var response = await _client!.SendAsync(request);
        return response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;
    }

    [Theory]
    [InlineData("http://localhost:5171")]          
    [InlineData("https://landing-85fe8.web.app")]  
    [InlineData("http://localhost:5173")]
    [InlineData("https://start.example.com")]      
    public async Task SitesThatMayCallTheGateway_AreAllowed(string origin) =>
        Assert.Equal(origin, await AllowedOrigin(origin));

    [Theory]
    [InlineData("http://localhost:5176")]
    [InlineData("https://evil.example.com")]       
    [InlineData("javascript:alert(1)")]
    public async Task OtherSites_AreNotAllowed(string origin) =>
        Assert.Null(await AllowedOrigin(origin));

    [Fact]
    public async Task GatewayRoot_ReturnsOk()
    {
        var response =
            await _client!.GetAsync("/");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content.ReadAsStringAsync();

        Assert.Equal(
            "AIWorkHub API Gateway",
            body);
    }

    [Fact]
    public async Task AuditRoute_ForwardsPathQueryAndAuthorization()
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/audit/logs?page=1&pageSize=5");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "test-token");

        var response =
            await _client!.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var payload =
            await response.Content
                .ReadFromJsonAsync<ForwardedRequest>();

        Assert.NotNull(payload);

        Assert.Equal(
            "/api/audit/logs",
            payload.Path);

        Assert.Equal(
            "?page=1&pageSize=5",
            payload.Query);

        Assert.Equal(
            "Bearer test-token",
            payload.Authorization);
    }

    [Fact]
    public async Task JiraRoute_ForwardsPathQueryAndAuthorization()
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/issues/search?page=1&pageSize=10");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "test-token");

        var response =
            await _client!.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var payload =
            await response.Content
                .ReadFromJsonAsync<ForwardedRequest>();

        Assert.NotNull(payload);

        Assert.Equal(
            "/api/issues/search",
            payload.Path);

        Assert.Equal(
            "?page=1&pageSize=10",
            payload.Query);

        Assert.Equal(
            "Bearer test-token",
            payload.Authorization);
    }

    [Fact]
    public async Task JiraIssueRoute_ForwardsSpecificIssue()
    {
        var response =
            await _client!.GetAsync(
                "/api/issues/14");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var payload =
            await response.Content
                .ReadFromJsonAsync<ForwardedRequest>();

        Assert.NotNull(payload);

        Assert.Equal(
            "/api/issues/14",
            payload.Path);
    }

    [Theory]
    [InlineData("/api/users?role=DEVELOPER")]
    [InlineData("/api/projects/2/members")]
    [InlineData("/api/notifications")]
    [InlineData("/api/notifications/unread-count")]
    [InlineData("/hubs/notifications/negotiate?negotiateVersion=1")]
    public async Task MembershipRoutesForwardBearerContext(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer","admin-token");
        using var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ForwardedRequest>();
        Assert.Equal(path.Split('?')[0],payload!.Path);
        Assert.Equal("Bearer admin-token",payload.Authorization);
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/signin-oidc?code=test&state=test")]
    public async Task LoginRoutesPreservePublicHost(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = "work.example.com";
        using var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ForwardedRequest>();
        Assert.Equal(path.Split('?')[0], payload!.Path);
        Assert.Equal("work.example.com", payload.Host);
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/auth/google")]
    [InlineData("/api/auth/logout")]
    public async Task AuthenticationRoutesPreserveTheSameCookieAndHost(string path) {
        using var request=new HttpRequestMessage(path.EndsWith("me")?HttpMethod.Get:HttpMethod.Post,path);
        request.Headers.Host="work.example.com";
        request.Headers.Add("Cookie","AIWorkHub.Session=test-session; AIWorkHub.GoogleNonce=test-nonce");
        using var response=await _client!.SendAsync(request);
        var payload=await response.Content.ReadFromJsonAsync<ForwardedRequest>();
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal("work.example.com",payload!.Host);
        Assert.Contains("AIWorkHub.Session=test-session",payload.Cookie);
        Assert.Contains("AIWorkHub.GoogleNonce=test-nonce",payload.Cookie);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFound()
    {
        var response =
            await _client!.GetAsync(
                "/api/does-not-exist");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private sealed record ForwardedRequest(
        string? Path,
        string? Query,
        string? Authorization,
        string? Host, string? Cookie);

    private sealed class GatewayFactory
        : WebApplicationFactory<Program>
    {
        private readonly string _backendAddress;

        public GatewayFactory(
            string backendAddress)
        {
            _backendAddress =
                backendAddress;
        }

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                {
                    var settings =
                        new Dictionary<string, string?>
                        {
                            ["Cors:ExtraOrigins:0"] = "https://start.example.com/",
                            ["Cors:ExtraOrigins:1"] = "javascript:alert(1)",
                            ["Cors:ExtraOrigins:2"] = "https://evil.example.com/path",

                            ["ReverseProxy:Routes:audit-route:ClusterId"]
                                = "audit-cluster",

                            ["ReverseProxy:Routes:audit-route:Match:Path"]
                                = "/api/audit/{**catch-all}",

                            ["ReverseProxy:Routes:jira-route:ClusterId"]
                                = "jira-cluster",

                            ["ReverseProxy:Routes:jira-route:Match:Path"]
                                = "/api/issues/{**catch-all}",

                            ["ReverseProxy:Clusters:identity-cluster:Destinations:identity-api:Address"] = _backendAddress,

                            ["ReverseProxy:Clusters:audit-cluster:Destinations:audit-api:Address"]
                                = _backendAddress,

                            ["ReverseProxy:Clusters:jira-cluster:Destinations:jira-api:Address"]
                                = _backendAddress
                        };

                    configuration.AddInMemoryCollection(
                        settings);
                });
        }
    }
}
