using System.Net;
using Jira.Api.Integrations.IdentityTenant;
using Jira.Application.Services;
using Microsoft.AspNetCore.Http;

namespace Backend.Authorization.Tests;

public sealed class AssignmentDirectoryFailureTests
{
    private sealed class ReplyHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
    }

    [Theory]
    [InlineData(503, "{}")]
    [InlineData(403, "{}")]
    [InlineData(200, "invalid-json")]
    public async Task DirectoryFailuresAreUnavailableInsteadOfGrantingAccess(int status, string body)
    {
        using var client = new HttpClient(new ReplyHandler((HttpStatusCode)status, body)) { BaseAddress = new Uri("https://identity.test/") };
        var context = new DefaultHttpContext(); context.Request.Headers.Authorization = "Bearer test-token";
        var directory = new DeveloperDirectoryClient(client, new HttpContextAccessor { HttpContext = context });
        await Assert.ThrowsAsync<UserDirectoryUnavailableException>(() => directory.GetAssignmentUserAsync(1,101,default));
    }

    [Theory]
    [InlineData("{\"id\":101,\"tenantId\":2,\"isAdmin\":true}")]
    [InlineData("{\"id\":102,\"tenantId\":1,\"isAdmin\":true}")]
    public async Task MismatchedDirectoryIdentityCannotGrantAdminAccess(string body)
    {
        using var client = new HttpClient(new ReplyHandler(HttpStatusCode.OK, body)) { BaseAddress = new Uri("https://identity.test/") };
        var context = new DefaultHttpContext(); context.Request.Headers.Authorization = "Bearer test-token";
        var directory = new DeveloperDirectoryClient(client, new HttpContextAccessor { HttpContext = context });
        Assert.Null(await directory.GetAssignmentUserAsync(1,101,default));
    }
}
