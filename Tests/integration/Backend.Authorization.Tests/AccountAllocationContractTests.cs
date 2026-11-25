using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;





public sealed class AccountAllocationContractTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null, uint? version = null, long tenant = 1, long user = 100, string role = "ADMIN")
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(user, tenant, role, SecurityFixture.Permissions));
        if (body is not null) request.Content = JsonContent.Create(body);
        if (version.HasValue) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await fixture.Client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string[] Keys(JsonElement element) => element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
    private static string[] Expect(params string[] keys) => keys.Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task AccountsKeepTheirExactShapeEtagAndRules()
    {
        var created = await Send("POST", "/api/projects/accounts", new { name = " Contract Co ", status = "ACTIVE" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = await Json(created);
        Assert.Equal(Expect("id", "tenantId", "name", "status", "version"), Keys(account));
        Assert.Equal("Contract Co", account.GetProperty("name").GetString());
        Assert.Equal($"\"{account.GetProperty("version").GetUInt32()}\"", created.Headers.ETag!.Tag);
        var id = account.GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", "/api/projects/accounts", new { name = "contract co" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("POST", "/api/projects/accounts", new { name = "Dev" }, user: 101, role: "DEVELOPER")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/api/projects/accounts", new { name = "" })).StatusCode);

        var updated = await Send("PUT", $"/api/projects/accounts/{id}", new { name = "Contract Co 2", status = "ACTIVE" }, account.GetProperty("version").GetUInt32());
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var renamed = await Json(updated);
        Assert.Equal(Expect("id", "tenantId", "name", "status", "version"), Keys(renamed));
        Assert.Equal($"\"{renamed.GetProperty("version").GetUInt32()}\"", updated.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await Send("PUT", $"/api/projects/accounts/{id}", new { name = "Stale" }, account.GetProperty("version").GetUInt32())).StatusCode);
        Assert.Equal((HttpStatusCode)428, (await Send("PUT", $"/api/projects/accounts/{id}", new { name = "No version" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("PUT", "/api/projects/accounts/999999", new { name = "Ghost" }, 1)).StatusCode);

        var list = await Json(await Send("GET", "/api/projects/accounts"));
        var mine = list.EnumerateArray().Single(a => a.GetProperty("id").GetInt64() == id);
        Assert.Equal(Expect("id", "name", "status", "version", "projectCount"), Keys(mine));
        Assert.Equal(0, mine.GetProperty("projectCount").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/api/projects/accounts/{id}", version: renamed.GetProperty("version").GetUInt32())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("DELETE", $"/api/projects/accounts/{id}", version: 1)).StatusCode);
    }

    [Fact]
    public async Task AllocationsKeepTheirExactShapeAndErrorBodies()
    {
        using (var scope = fixture.App.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<JiraDbContext>().Database.ExecuteSqlRawAsync(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "025_allocations.sql")));

        object Body(long project, int percent, string start = "2031-01-01", string? end = null) => new { projectId = project, userId = 100, percentage = percent, startDate = start, endDate = end };

        var created = await Send("POST", "/api/projects/allocations", Body(1, 60));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(0, (await created.Content.ReadAsByteArrayAsync()).Length);

        var dates = await Send("POST", "/api/projects/allocations", Body(1, 10, "2031-02-01", "2031-01-01"));
        Assert.Equal(HttpStatusCode.BadRequest, dates.StatusCode);
        Assert.Equal("Enter a valid allocation date range.", (await dates.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());

        var overlap = await Send("POST", "/api/projects/allocations", Body(1, 10));
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        Assert.Equal("This project already has an overlapping allocation.", (await overlap.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());

        var capacity = await Send("POST", "/api/projects/allocations", Body(2, 50));
        Assert.Equal(HttpStatusCode.Conflict, capacity.StatusCode);
        Assert.Equal("Total allocation cannot exceed 100%.", (await capacity.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());

        var missing = await Send("POST", "/api/projects/allocations", Body(99, 10));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var rows = await Json(await Send("GET", "/api/projects/allocations"));
        var row = rows.EnumerateArray().Single();
        Assert.Equal(Expect("id", "projectId", "projectName", "userId", "percentage", "startDate", "endDate"), Keys(row));
        Assert.Equal("2031-01-01", row.GetProperty("startDate").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("endDate").ValueKind);
        Assert.Equal(60, row.GetProperty("percentage").GetInt32());

        Assert.Equal(HttpStatusCode.Forbidden, (await Send("GET", "/api/projects/allocations?userId=100", user: 101, role: "DEVELOPER")).StatusCode);
        var id = row.GetProperty("id").GetInt64();
        var removed = await Send("DELETE", $"/api/projects/allocations/{id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("DELETE", $"/api/projects/allocations/{id}")).StatusCode);
        Assert.Equal(0, (await Json(await Send("GET", "/api/projects/allocations"))).GetArrayLength());
    }
}
