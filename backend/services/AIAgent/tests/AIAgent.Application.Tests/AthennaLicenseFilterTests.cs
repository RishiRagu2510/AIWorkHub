using AIAgent.Api.Controllers;
using AIAgent.Api.Licensing;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace AIAgent.Application.Tests;





public sealed class AthennaLicenseFilterTests
{
    private sealed class Identity(VerifiedCaller? caller, bool credentials = true) : ICallerIdentityProvider
    {
        public bool HasCredentials => credentials;
        public Task<VerifiedCaller?> VerifyAsync(CancellationToken cancellationToken = default) => Task.FromResult(caller);
    }

    private static VerifiedCaller Caller(params string[]? features) =>
        new(7, "DEVELOPER", ["DEVELOPER"], ["ISSUE_VIEW"], 1, features);

    
    private static async Task<(IActionResult? Result, bool Reached)> Run(ICallerIdentityProvider identity, bool allowWithoutLicense = false)
    {
        var metadata = allowWithoutLicense ? new object[] { new AllowWithoutAthennaLicenseAttribute() } : [];
        var action = new ActionDescriptor { EndpointMetadata = metadata };
        var http = new DefaultHttpContext();
        var actionContext = new ActionContext(http, new RouteData(), action);
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: new object());
        var reached = false;
        Task<ActionExecutedContext> Next()
        {
            reached = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], controller: new object()));
        }
        await new AthennaLicenseFilter(identity).OnActionExecutionAsync(executing, Next);
        return (executing.Result, reached);
    }

    [Fact]
    public async Task ACompanyWithNoPlanSetting_IsServed()
    {
        var (result, reached) = await Run(new Identity(Caller()));
        Assert.Null(result);
        Assert.True(reached);
    }

    [Fact]
    public async Task ACompanyWhosePlanIncludesAthenna_IsServed()
    {
        var (result, reached) = await Run(new Identity(Caller("athenna")));
        Assert.Null(result);
        Assert.True(reached);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("somethingelse")]
    public async Task ACompanyWhosePlanLeavesAthennaOut_IsRefusedBeforeTheEndpointRuns(string listed)
    {
        var (result, reached) = await Run(new Identity(Caller(listed)));
        var refusal = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, refusal.StatusCode);
        Assert.Contains("FEATURE_NOT_LICENSED", refusal.Value!.ToString());
        Assert.False(reached);
    }

    private static VerifiedCaller CallerWithPortals(string[] portals, params string[]? features) =>
        new(7, "DEVELOPER", ["DEVELOPER"], ["ISSUE_VIEW"], 1, features, portals);

    [Theory]
    [InlineData("workspace,employee", null)]        
    [InlineData("workspace,employee", "athenna")]   
    public async Task ACompanyWithoutAIWorkHub_IsRefused_BecauseTheChatComesWithIt(string portals, string? features)
    {
        var (result, reached) = await Run(new Identity(CallerWithPortals(portals.Split(','), features?.Split(','))));

        var refusal = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, refusal.StatusCode);
        Assert.Contains("FEATURE_NOT_LICENSED", refusal.Value!.ToString());
        Assert.False(reached);
    }

    [Theory]
    [InlineData("workspace,employee,hub", null)]
    [InlineData("workspace,employee,aiworkhub", "athenna")]
    public async Task ACompanyWithAIWorkHub_IsServed(string portals, string? features)
    {
        var (result, reached) = await Run(new Identity(CallerWithPortals(portals.Split(','), features?.Split(','))));
        Assert.Null(result);
        Assert.True(reached);
    }

    [Fact]
    public async Task TheStatusCheck_StaysOpenForACompanyWithoutAIWorkHub_SoThePageCanExplainWhy()
    {
        var (result, reached) = await Run(new Identity(CallerWithPortals(["workspace"])), allowWithoutLicense: true);
        Assert.Null(result);
        Assert.True(reached);
    }

    [Fact]
    public async Task TheStatusCheck_StaysOpenWithoutALicence()
    {
        var (result, reached) = await Run(new Identity(Caller("none")), allowWithoutLicense: true);
        Assert.Null(result);
        Assert.True(reached);
    }

    [Fact]
    public async Task ARequestWithNoVerifiableCaller_IsLeftToTheEndpointToAnswer401()
    {
        Assert.True((await Run(new Identity(null))).Reached);
        Assert.True((await Run(new Identity(null, credentials: false))).Reached);
    }

    [Fact]
    public void OnlyTheStatusEndpoint_IsMarkedOpen()
    {
        var open = typeof(AthennaAgentController).GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(AllowWithoutAthennaLicenseAttribute), false).Length > 0)
            .Select(m => m.Name)
            .ToArray();
        Assert.Equal(["GetStatus"], open);
    }
}
