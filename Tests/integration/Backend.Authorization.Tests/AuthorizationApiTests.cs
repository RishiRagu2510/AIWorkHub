using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Authentication;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Application.Services;
using IdentityTenant.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TenantContext;

namespace Backend.Authorization.Tests;

public sealed class AuthorizationApiTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, long user = 101, long tenant = 1,
        string role = "DEVELOPER", object? body = null, string[]? permissions = null, bool anonymous = false)
    {
        var request = new HttpRequestMessage(new HttpMethod(method),path);
        if (!anonymous) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",fixture.Token(user,tenant,role,permissions));
        if (body is not null) request.Content = JsonContent.Create(body);
        await VersionedTestRequests.AddCurrentVersionAsync(fixture.Client, request);
        return await fixture.Client.SendAsync(request);
    }

    [Theory]
    [InlineData(101,1,"DEVELOPER",10)]
    [InlineData(102,1,"DEVELOPER",11,12)]
    [InlineData(100,1,"ADMIN",10,11,12)]
    [InlineData(201,2,"DEVELOPER",20)]
    [InlineData(200,2,"ADMIN",20)]
    [InlineData(104,1,"DEVELOPER")]
    [InlineData(105,1,"DEVELOPER")]
    [InlineData(105,1,"PROJECT_MANAGER",12)]
    [InlineData(106,1,"PROJECT_MANAGER")]
    public async Task SearchAndDetailsRespectEffectiveScope(long user, long tenant, string role, params int[] expected)
    {
        using var response = await Send("GET","/api/issues/search?pageSize=100",user,tenant,role);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected.Order(),json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).Order());
        Assert.Equal(expected.Length,json.GetProperty("totalCount").GetInt32());
        foreach (var id in new[] {10,11,12,20})
        {
            using var detail = await Send("GET",$"/api/issues/{id}",user,tenant,role);
            Assert.Equal(expected.Contains(id) ? HttpStatusCode.OK : HttpStatusCode.NotFound, detail.StatusCode);
        }
    }

    [Theory]
    [InlineData("GET", "/api/issues/12/comments")]
    [InlineData("POST", "/api/issues/12/comments")]
    [InlineData("PUT", "/api/issues/12")]
    [InlineData("PUT", "/api/issues/12/assignee")]
    [InlineData("PUT", "/api/issues/12/transition")]
    [InlineData("DELETE", "/api/issues/12")]
    public async Task SharedProjectMembershipDoesNotGrantDeveloperAccessToOthersWork(string method, string path)
    {
        object? body = method == "POST" ? new {body="Forbidden"} : method == "PUT" ? new {summary="Forbidden",assigneeUserId=104,statusCode="DONE"} : null;
        using var response = await Send(method,path,104,1,"DEVELOPER",body);
        Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
    }

    [Fact]
    public async Task FiltersAndClientSecurityValuesCannotWidenScope()
    {
        using var response = await Send("GET","/api/issues/search?assigneeUserId=102&tenant_id=2&user_id=102&role=ADMIN&permissions=ISSUE_VIEW");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0,json.GetProperty("totalCount").GetInt32());
        using var page = await Send("GET","/api/issues/search?pageSize=1",100,1,"ADMIN");
        var paged = await page.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3,paged.GetProperty("totalCount").GetInt32());
        Assert.Single(paged.GetProperty("items").EnumerateArray());
    }

    public static IEnumerable<object[]> RestrictedEndpoints()
    {
        foreach (var id in new[] {11,20})
        {
            yield return ["GET",$"/api/issues/{id}",null!];
            yield return ["GET",$"/api/issues/key/{(id == 11 ? "A-11" : "C-20")}",null!];
            yield return ["GET",$"/api/issues/{id}/comments",null!];
            yield return ["POST",$"/api/issues/{id}/comments",new {body="Forbidden"}];
            yield return ["PUT",$"/api/issues/{id}",new {summary="Forbidden"}];
            yield return ["PUT",$"/api/issues/{id}/assignee",new {assigneeUserId=101}];
            yield return ["PUT",$"/api/issues/{id}/transition",new {statusCode="DONE"}];
            yield return ["DELETE",$"/api/issues/{id}",null!];
        }
        yield return ["POST","/api/issues",new {projectId=3,summary="Forbidden"}];
        yield return ["POST","/api/issues",new {projectId=2,summary="Forbidden"}];
    }

    [Theory]
    [MemberData(nameof(RestrictedEndpoints))]
    public async Task RestrictedResourcesRejectReadAndWrite(string method,string path,object? body)
    {
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        var outboxCount = await db.OutboxMessages.CountAsync();
        using var response = await Send(method,path,body:body);
        Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
        Assert.False(await db.Issues.AnyAsync(i => i.Summary == "Forbidden"));
        Assert.False(await db.IssueComments.AnyAsync(i => i.Body == "Forbidden"));
        Assert.Equal(outboxCount, await db.OutboxMessages.CountAsync());
    }

    [Theory]
    [InlineData("GET","/api/issues/20")]
    [InlineData("GET","/api/issues/key/C-20")]
    [InlineData("GET","/api/issues/20/comments")]
    [InlineData("POST","/api/issues/20/comments")]
    [InlineData("PUT","/api/issues/20")]
    [InlineData("PUT","/api/issues/20/assignee")]
    [InlineData("PUT","/api/issues/20/transition")]
    [InlineData("DELETE","/api/issues/20")]
    [InlineData("GET","/api/projects/3")]
    public async Task AdminCannotCrossTenant(string method,string path)
    {
        object? body = method == "POST" ? new {body="Forbidden"} : method == "PUT" ? new {summary="Forbidden",assigneeUserId=100,statusCode="DONE"} : null;
        using var response = await Send(method,path,100,1,"ADMIN",body);
        Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
    }

    [Theory]
    [InlineData("GET","/api/issues/search")]
    [InlineData("GET","/api/issues/10")]
    [InlineData("GET","/api/issues/key/A-10")]
    [InlineData("GET","/api/issues/project/1")]
    [InlineData("POST","/api/issues")]
    [InlineData("PUT","/api/issues/10")]
    [InlineData("PUT","/api/issues/10/assignee")]
    [InlineData("PUT","/api/issues/10/transition")]
    [InlineData("DELETE","/api/issues/10")]
    [InlineData("GET","/api/issues/10/comments")]
    [InlineData("POST","/api/issues/10/comments")]
    [InlineData("GET","/api/projects")]
    [InlineData("GET","/api/projects/1")]
    [InlineData("GET","/api/audit/logs")]
    public async Task EndpointsRequireAuthenticationAndPermission(string method,string path)
    {
        using var anonymous = await Send(method,path,anonymous:true);
        Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
        using var denied = await Send(method,path,permissions:[]);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
    }

    [Fact]
    public async Task GeneralUpdateCannotBypassAssignOrTransitionPermission()
    {
        foreach (var body in new object[] {new {assigneeUserId=102},new {statusCode="DONE"}})
        {
            using var response = await Send("PUT","/api/issues/10",body:body,permissions:["ISSUE_UPDATE"]);
            Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        }
    }

    [Fact]
    public async Task ProjectsProjectListsAndCommentsHaveSameScope()
    {
        using var projects = await Send("GET","/api/projects");
        var list = await projects.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] {1},list.EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
        using var issues = await Send("GET","/api/issues/project/1");
        var items = await issues.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] {10},items.EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
        using var cross = await Send("GET","/api/issues/project/3",100,1,"ADMIN");
        Assert.Empty((await cross.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        using var comments = await Send("GET","/api/issues/10/comments");
        Assert.Equal(HttpStatusCode.OK,comments.StatusCode);
        using var other = await Send("GET","/api/tenants/2",100,1,"ADMIN");
        Assert.Equal(HttpStatusCode.Forbidden,other.StatusCode);
    }

    [Fact]
    public async Task AuditUsesPermissionAndAuthenticatedTenant()
    {
        using var denied = await Send("GET","/api/audit/logs",permissions:["ISSUE_VIEW"]);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        foreach (var tenant in new long[] {1,2})
        {
            using var response = await Send("GET",$"/api/audit/logs?tenantId={3-tenant}",100,tenant,"ADMIN");
            var page = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Single(page.GetProperty("items").EnumerateArray());
            Assert.All(page.GetProperty("items").EnumerateArray(),item => Assert.Equal(tenant,item.GetProperty("tenantId").GetInt64()));
        }
    }

    [Theory]
    [InlineData("user101@COMPANY.com","DEVELOPER",101,1)]
    [InlineData("user100@company.com","ADMIN",100,1)]
    [InlineData("user200@other.com","ADMIN",200,2)]
    public async Task VerifiedCompanyLoginLoadsDatabaseRoleAndIssuesCorrectJwt(string email,string role,long user,long tenant)
    {
        using var scope = fixture.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>();
        if(tenant==2) service=ActivatorUtilities.CreateInstance<CompanyPrincipalService>(scope.ServiceProvider,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Authentication:Google:ClientId"]="other-client"}).Build());
        var principal = await service.SignInGoogleAsync(SecurityFixture.Google(email),default);
        Assert.Equal(new[] {role},principal.FindAll(TenantClaimTypes.Role).Select(c => c.Value));
        Assert.Equal(user.ToString(),principal.FindFirstValue(TenantClaimTypes.UserId));
        Assert.Equal(tenant.ToString(),principal.FindFirstValue(TenantClaimTypes.TenantId));
        var token = fixture.App.Services.GetRequiredService<IAccessTokenService>().CreateAccessToken(principal);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(new[] {role},jwt.Claims.Where(c => c.Type == TenantClaimTypes.Role).Select(c => c.Value));
        Assert.Equal(tenant.ToString(),jwt.Claims.Single(c => c.Type == TenantClaimTypes.TenantId).Value);
        var request = new HttpRequestMessage(HttpMethod.Get,"/api/issues/search");
        request.Headers.Authorization = new("Bearer",token);
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
    }

    [Theory]
    [InlineData("developer@gmail.com",true,"ORGANIZATION_ACCESS_DENIED")]
    [InlineData("user101@company.com",false,"EMAIL_NOT_VERIFIED")]
    public async Task RejectedGoogleIdentityCannotProduceLocalPrincipal(string email,bool verified,string code)
    {
        using var scope = fixture.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>();
        var exception = await Assert.ThrowsAsync<LoginRejectedException>(() => service.SignInGoogleAsync(SecurityFixture.Google(email,verified),default));
        Assert.Equal(code,exception.Code);
        var db = scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
        Assert.False(await db.UserExternalIdentities.AnyAsync(i => i.ExternalSubject == email));
    }

    [Fact]
    public async Task InvitedUserRemainsWithoutPermissionsUntilAdministratorAssignsRoles()
    {
        using var scope = fixture.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
        const string email = "newdeveloper@gmail.com";
        db.Add(new IdentityTenant.Domain.Entities.TenantInvitation {TenantId=1,Email=email,InvitedByUserId=100,ExpiresAt=DateTimeOffset.UtcNow.AddDays(1)});
        await db.SaveChangesAsync();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var principal = await service.SignInGoogleAsync(SecurityFixture.Google(email),default);
            Assert.Empty(principal.FindAll(TenantClaimTypes.Role));
            Assert.Equal("1",principal.FindFirstValue(TenantClaimTypes.TenantId));
            Assert.DoesNotContain(principal.FindAll(TenantClaimTypes.Permission), c => c.Value == "AUDIT_VIEW");
            var jwt = fixture.App.Services.GetRequiredService<IAccessTokenService>().CreateAccessToken(principal);
            var request = new HttpRequestMessage(HttpMethod.Get,"/api/issues/search");
            request.Headers.Authorization = new("Bearer",jwt);
            using var response = await fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        }
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.True(user.EmailVerified);
        Assert.Equal(0,await db.UserRoles.CountAsync(r => r.UserId == user.Id));
        Assert.Equal(1,await db.UserExternalIdentities.CountAsync(i => i.UserId == user.Id));
    }

    [Fact]
    public async Task ExistingCookiesCannotRetainForgedOrStaleAdminRole()
    {
        using var scope = fixture.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>();
        var principal = await service.SignInGoogleAsync(SecurityFixture.Google("user102@company.com"),default);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(TenantClaimTypes.Role,"ADMIN"));
        var refreshed = await service.RefreshAsync(principal,default);
        Assert.NotNull(refreshed);
        Assert.Equal(new[] {"DEVELOPER"},refreshed.FindAll(TenantClaimTypes.Role).Select(c => c.Value));
        ((ClaimsIdentity)principal.Identity!).RemoveClaim(principal.FindFirst("aiworkhub:company_verified")!);
        Assert.Null(await service.RefreshAsync(principal,default));
    }

    [Theory]
    [InlineData("stranger@gmail.com",true,"ORGANIZATION_ACCESS_DENIED")]
    [InlineData("user101@company.com",false,"EMAIL_NOT_VERIFIED")]
    public async Task GoogleCallbackReturns401AndStopsCookieSignIn(string email,bool verified,string code)
    {
        using var scope = fixture.App.Services.CreateScope();
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Response.Body = new MemoryStream();
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(SecurityFixture.Google(email,verified),"Google");
        var context = new Microsoft.AspNetCore.Authentication.TicketReceivedContext(http,
            new Microsoft.AspNetCore.Authentication.AuthenticationScheme("Google",null,typeof(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectHandler)),
            new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions(),ticket);
        await CompanyGoogleTicket.HandleAsync(context);
        Assert.Equal(401,http.Response.StatusCode);
        Assert.True(context.Result!.Handled);
        Assert.False(http.Response.Headers.ContainsKey("Set-Cookie"));
        http.Response.Body.Position = 0;
        var error = await JsonSerializer.DeserializeAsync<JsonElement>(http.Response.Body);
        Assert.Equal(code,error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthorizedWritesUseAuthenticatedActorAndTenant()
    {
        using var created = await Send("POST","/api/issues",body:new {projectId=1,summary="Authorized lifecycle",tenantId=2,reporterUserId=102,userId=102,role="ADMIN"});
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var issue = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = issue.GetProperty("id").GetInt64();
        Assert.Equal(1,issue.GetProperty("tenantId").GetInt64());
        Assert.Equal(101,issue.GetProperty("reporterUserId").GetInt64());
        using var updated = await Send("PUT",$"/api/issues/{id}",body:new {summary="Updated by owner"});
        Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        using var member = await Send("POST","/api/projects/1/members",user:100,role:"ADMIN",body:new {userId=102});
        Assert.Equal(HttpStatusCode.NoContent,member.StatusCode);
        using var assigned = await Send("PUT",$"/api/issues/{id}/assignee",body:new {assigneeUserId=102});
        Assert.Equal(HttpStatusCode.OK,assigned.StatusCode);
        using var transitioned = await Send("PUT",$"/api/issues/{id}/transition",body:new {statusCode="DONE",confirmSkip=true});
        Assert.Equal(HttpStatusCode.OK,transitioned.StatusCode);
        using var commented = await Send("POST",$"/api/issues/{id}/comments",body:new {body="Owner comment",authorUserId=102,tenantId=2});
        Assert.Equal(HttpStatusCode.OK,commented.StatusCode);
        var comment = await commented.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(101,comment.GetProperty("authorUserId").GetInt64());
        using var deleted = await Send("DELETE",$"/api/issues/{id}");
        Assert.Equal(HttpStatusCode.OK,deleted.StatusCode);
        using var hidden = await Send("GET",$"/api/issues/{id}");
        Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
    }

    [Fact]
    public async Task DomainIndexRejectsCaseInsensitiveDuplicate()
    {
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE tenants SET email_domain = 'COMPANY.COM' WHERE id = 2"));
    }
}
