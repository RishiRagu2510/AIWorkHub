using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Authentication;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;

public sealed class AccountProjectTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null,
        long tenant = 1, long user = 100, string role = "ADMIN", string[]? permissions = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(user, tenant, role, permissions ?? ["PROJECT_VIEW", "PROJECT_MANAGE"]));
        if (body != null) request.Content = JsonContent.Create(body);
        await VersionedTestRequests.AddCurrentVersionAsync(fixture.Client, request);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task BankingProjectsCanBeCreatedAndUpdated()
    {
        using var create = await Send(HttpMethod.Post, "/api/projects", new { accountName = "Banking account", projectName = "Banking portal", projectType = "BANKING" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var project = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BANKING", project.GetProperty("projectType").GetString());
        var id = project.GetProperty("id").GetInt64();
        using var update = await Send(HttpMethod.Put, $"/api/projects/{id}", new { name = "Banking account", projectType = "BANKING", description = "Banking services" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("BANKING", (await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("projectType").GetString());
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        Assert.Equal(Jira.Domain.Enums.ProjectType.Banking, (await db.Projects.AsNoTracking().SingleAsync(p => p.Id == id)).ProjectType);
    }

    [Fact]
    public async Task AccountMigrationPreservesProjectsAndIsRepeatable()
    {
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        var before = await db.Projects.AsNoTracking().Select(p => new {p.Id, p.Key, p.TenantId}).ToArrayAsync();
        var issues = await db.Issues.CountAsync();
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "020_accounts.sql"));
        await db.Database.ExecuteSqlRawAsync(sql);
        var count = await db.Accounts.CountAsync();
        await db.Database.ExecuteSqlRawAsync(sql);
        Assert.Equal(count, await db.Accounts.CountAsync());
        Assert.Equal(issues, await db.Issues.CountAsync());
        var after = await db.Projects.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();
        foreach (var project in before)
        {
            var migrated = Assert.Single(after, p => p.Id == project.Id);
            Assert.Equal(project.Key, migrated.Key);
            Assert.Equal(project.TenantId, migrated.TenantId);
            Assert.NotNull(migrated.AccountId);
        }
    }

    [Fact]
    public async Task AccountIsReusedForMultipleProjectsAndTenantIsAuthoritative()
    {
        var name = "Account " + Guid.NewGuid().ToString("N");
        using var first = await Send(HttpMethod.Post, "/api/projects", new { accountName = name, projectName = "A" + Guid.NewGuid().ToString("N"), tenantId = 2 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = a.GetProperty("accountId").GetInt64();
        Assert.Equal(1, a.GetProperty("tenantId").GetInt64());
        using var second = await Send(HttpMethod.Post, "/api/projects", new { accountName = name.ToLowerInvariant(), projectName = "B" + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var b = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(accountId, b.GetProperty("accountId").GetInt64());
        Assert.Equal(name, b.GetProperty("accountName").GetString());
        using var third = await Send(HttpMethod.Post, "/api/projects", new { accountId, accountName = name, projectName = "C" + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.Created, third.StatusCode);
        using var list = await Send(HttpMethod.Get, "/api/projects/accounts");
        var accounts = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, accounts.EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == accountId).GetProperty("projectCount").GetInt32());
        using var foreign = await Send(HttpMethod.Post, "/api/projects", new { accountId, accountName = name, projectName = "FOREIGN" }, tenant:2, user:200);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var developer = await Send(HttpMethod.Get, "/api/projects/accounts", user:101, role:"DEVELOPER");
        var visible = await developer.Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(visible.EnumerateArray(), x => x.GetProperty("id").GetInt64() == accountId);
        using var forbidden = await Send(HttpMethod.Post, "/api/projects", new { accountName = name, projectName = "FORBIDDEN" }, user:101, role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task DuplicateProjectRollsBackNewAccountAndLegacyFieldsStillWork()
    {
        var key = "D" + Guid.NewGuid().ToString("N");
        using var first = await Send(HttpMethod.Post, "/api/projects", new { name = "Legacy account", key });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var name = "Rollback " + Guid.NewGuid().ToString("N");
        using var duplicate = await Send(HttpMethod.Post, "/api/projects", new { accountName = name, projectName = key });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        Assert.False(await db.Accounts.AnyAsync(a => a.Name == name));
    }
}
