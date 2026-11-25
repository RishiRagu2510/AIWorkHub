using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Audit.Application.Services;
using Jira.Domain.Entities;
using Jira.Infrastructure.Persistence;
using Messaging.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Authorization.Tests;

public sealed class ConcurrencyAndProjectAuditTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null, string? version = null, long tenant = 1)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",fixture.Token(tenant == 1 ? 100 : 200,tenant,"ADMIN"));
        if (version is not null) request.Headers.TryAddWithoutValidation("If-Match",version);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }
    private async Task<(long Id,string Tag)> Project()
    {
        using var response = await Send("POST","/api/projects",new {key="P"+Guid.NewGuid().ToString("N"), name="Original",tenantId=2,performedByUserId=200});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tag = response.Headers.ETag!.Tag;
        Assert.Equal($"\"{json.GetProperty("version").GetUInt32()}\"",tag);
        return (json.GetProperty("id").GetInt64(),tag);
    }
    private async Task<(long Id,string Tag)> Issue(long project)
    {
        using var response = await Send("POST","/api/issues",new {projectId=project,summary="Original"});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("id").GetInt64(), response.Headers.ETag!.Tag);
    }
    private async Task<List<IssueAuditEvent>> Events(long project)
    {
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        var payloads = await db.OutboxMessages.AsNoTracking().OrderBy(m=>m.Id).Select(m=>m.Payload).ToListAsync();
        return payloads.Select(p=>JsonSerializer.Deserialize<IssueAuditEvent>(p)!).Where(e=>e.EntityType=="PROJECT" && e.EntityId==project).ToList();
    }

    [Fact]
    public async Task StaleProjectEditMoveAndDeleteCannotOverwriteNewerChanges()
    {
        var (id,tag) = await Project();
        using var update = await Send("PUT",$"/api/projects/{id}",new {name="Newer"},tag);
        Assert.Equal(HttpStatusCode.OK,update.StatusCode);
        Assert.NotEqual(tag,update.Headers.ETag!.Tag);
        foreach (var (method,path,body) in new (string,string,object?)[] {
            ("PUT",$"/api/projects/{id}",new {name="Stale"}),
            ("PUT",$"/api/projects/{id}/parent",new {parentProjectId=(long?)null}),
            ("DELETE",$"/api/projects/{id}",null) })
        {
            using var stale = await Send(method,path,body,tag);
            Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);
            Assert.Equal("STALE_VERSION",(await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        using var read = await Send("GET",$"/api/projects/{id}");
        Assert.Equal("Newer",(await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString());
        Assert.Equal(new[] {"PROJECT_CREATED","PROJECT_UPDATED"},(await Events(id)).Select(e=>e.EventType));
    }

    [Fact]
    public async Task StaleIssueEditAssignmentTransitionAndDeleteAreRejected()
    {
        var (project,_) = await Project(); var (id,tag) = await Issue(project);
        using var first = await Send("PUT",$"/api/issues/{id}",new {summary="Newer"},tag);
        Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        foreach (var (method,path,body) in new (string,string,object?)[] {
            ("PUT",$"/api/issues/{id}",new {summary="Stale"}),
            ("PUT",$"/api/issues/{id}/assignee",new {assigneeUserId=100}),
            ("PUT",$"/api/issues/{id}/transition",new {statusCode="DONE"}),
            ("DELETE",$"/api/issues/{id}",null) })
        {
            using var stale = await Send(method,path,body,tag);
            Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);
        }
        using var read = await Send("GET",$"/api/issues/{id}");
        Assert.Equal("Newer",(await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("summary").GetString());
    }

    [Theory]
    [InlineData("projects")]
    [InlineData("issues")]
    public async Task TwoSimultaneousWritesWithSameVersionHaveExactlyOneWinner(string resource)
    {
        var target = await Project();
        if (resource=="issues") target = await Issue(target.Id);
        object body = resource=="issues" ? new {summary="Concurrent"} : new {name="Concurrent"};
        var responses = await Task.WhenAll(Send("PUT",$"/api/{resource}/{target.Id}",body,target.Tag),Send("PUT",$"/api/{resource}/{target.Id}",body,target.Tag));
        try
        {
            Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);
            Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.PreconditionFailed);
        }
        finally { foreach(var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData(null,428)]
    [InlineData("*",400)]
    [InlineData("W/\"123\"",400)]
    [InlineData("123",400)]
    [InlineData("\"1\",\"2\"",400)]
    [InlineData("\"0\"",400)]
    public async Task ProjectAndIssueWritesRequireASingleStrongVersion(string? version,int status)
    {
        using var project = await Send("PUT","/api/projects/1",new {name="Rejected"},version);
        using var issue = await Send("PUT","/api/issues/10",new {summary="Rejected"},version);
        Assert.Equal(status,(int)project.StatusCode);
        Assert.Equal(status,(int)issue.StatusCode);
    }

    [Fact]
    public async Task CrossTenantVersionDoesNotRevealResource()
    {
        var (id,tag) = await Project();
        using var other = await Send("PUT",$"/api/projects/{id}",new {name="Forbidden"},tag,tenant:2);
        Assert.Equal(HttpStatusCode.NotFound,other.StatusCode);
    }

    [Fact]
    public async Task DatabaseTokenDetectsChangesEvenOutsideHttpWriteFlow()
    {
        var (id,_) = await Project();
        using var scope1 = fixture.App.Services.CreateScope(); using var scope2 = fixture.App.Services.CreateScope();
        var first = scope1.ServiceProvider.GetRequiredService<JiraDbContext>();
        var second = scope2.ServiceProvider.GetRequiredService<JiraDbContext>();
        var a = await first.Projects.SingleAsync(p=>p.Id==id); var b = await second.Projects.SingleAsync(p=>p.Id==id);
        a.Update("First",null,a.ProjectType); await first.SaveChangesAsync();
        b.Update("Second",null,b.ProjectType);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>second.SaveChangesAsync());
    }

    [Fact]
    public async Task ProjectAndMembershipHistoryPreservesActorAndSnapshotsIncludingDeletion()
    {
        var (id,tag) = await Project(); var (parent,_) = await Project();
        using var edited = await Send("PUT",$"/api/projects/{id}",new {name="Original",displayName="Edited",status="ARCHIVED",actorUserId=200},tag);
        Assert.Equal(HttpStatusCode.OK,edited.StatusCode);
        using var moved = await Send("PUT",$"/api/projects/{id}/parent",new {parentProjectId=parent},edited.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.OK,moved.StatusCode);
        for(var i=0;i<2;i++)
        {
            using var added = await Send("POST",$"/api/projects/{id}/members",new {userId=101});
            Assert.Equal(HttpStatusCode.NoContent,added.StatusCode);
        }
        for(var i=0;i<2;i++)
        {
            using var removed = await Send("DELETE",$"/api/projects/{id}/members/101");
            Assert.Equal(HttpStatusCode.NoContent,removed.StatusCode);
        }
        using var deleted = await Send("DELETE",$"/api/projects/{id}",version:moved.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NoContent,deleted.StatusCode);
        var events = await Events(id);
        Assert.Equal(new[] {"PROJECT_CREATED","PROJECT_UPDATED","PROJECT_MOVED","PROJECT_MEMBER_ADDED","PROJECT_MEMBER_REMOVED","PROJECT_DELETED"},events.Select(e=>e.EventType));
        Assert.All(events,e=> {Assert.Equal(1,e.TenantId);Assert.Equal(100,e.PerformedByUserId);Assert.NotEqual(Guid.Empty,e.EventId);});
        Assert.Equal("Original",JsonDocument.Parse(events[1].OldValues!).RootElement.GetProperty("Name").GetString());
        Assert.Equal("ARCHIVED",JsonDocument.Parse(events[1].NewValues!).RootElement.GetProperty("Status").GetString());
        Assert.Equal(parent,JsonDocument.Parse(events[2].NewValues!).RootElement.GetProperty("ParentProjectId").GetInt64());
        Assert.Null(events[^1].NewValues);
        
        using var scope = fixture.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AuditLogService>();
        foreach(var e in events)
        {
            for(var attempt=0;attempt<2;attempt++)
                Assert.Equal(attempt==0,await service.RecordAsync(e.EventId,e.EventType,e.TenantId,e.PerformedByUserId,e.EntityType,e.EntityId,e.EntityKey,"JIRA",e.OldValues,e.NewValues,e.Metadata,e.OccurredAt));
        }
        using var history = await Send("GET",$"/api/audit/logs?entityType=PROJECT&entityId={id}");
        Assert.Equal(HttpStatusCode.OK,history.StatusCode);
        Assert.Equal(6,(await history.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
        using var otherTenant = await Send("GET",$"/api/audit/logs?entityType=PROJECT&entityId={id}",tenant:2);
        Assert.Equal(0,(await otherTenant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task FailedAuditInsertRollsBackProjectEditAndMembershipInsert()
    {
        var (id,tag) = await Project();
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_project_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.event_type IN ('PROJECT_UPDATED','PROJECT_MEMBER_ADDED') THEN RAISE EXCEPTION 'Test audit failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER reject_project_audit BEFORE INSERT ON outbox_messages FOR EACH ROW EXECUTE FUNCTION reject_project_audit();
            """);
        try
        {
            using var update = await Send("PUT",$"/api/projects/{id}",new {name="Must roll back"},tag);
            Assert.Equal(HttpStatusCode.InternalServerError,update.StatusCode);
            using var member = await Send("POST",$"/api/projects/{id}/members",new {userId=101});
            Assert.Equal(HttpStatusCode.InternalServerError,member.StatusCode);
            Assert.False(await db.ProjectMembers.AnyAsync(m=>m.ProjectId==id));
            Assert.Equal("Original",(await db.Projects.AsNoTracking().SingleAsync(p=>p.Id==id)).Name);
            Assert.Single(await Events(id));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_project_audit ON outbox_messages; DROP FUNCTION reject_project_audit();");
        }
    }
}
