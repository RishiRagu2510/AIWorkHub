using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Jira.Api.Collaboration;
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Collaboration;
using Kafka;
using Messaging.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;

public sealed class CollaborationApiTests(SecurityFixture fixture):IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method,string path,object? body=null,long user=100,string role="ADMIN",string? key=null,string? tag=null)
    {
        using var r=new HttpRequestMessage(new HttpMethod(method),path);r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,1,role));
        if(body is not null)r.Content=JsonContent.Create(body);
        if(key is not null)r.Headers.Add("Idempotency-Key",key);
        if(tag is not null)r.Headers.TryAddWithoutValidation("If-Match",tag);
        else await VersionedTestRequests.AddCurrentVersionAsync(fixture.Client,r);
        return await fixture.Client.SendAsync(r);
    }
    private async Task<long> Project()
    {
        using var r=await Send("POST","/api/projects",new{key="C"+Guid.NewGuid().ToString("N"),name="Collaboration"});Assert.Equal(HttpStatusCode.Created,r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }
    private async Task<long> Issue(long project,long? assignee=null)
    {
        if(assignee.HasValue){using var add=await Send("POST",$"/api/projects/{project}/members",new{userId=assignee});Assert.Equal(HttpStatusCode.NoContent,add.StatusCode);}
        using var r=await Send("POST","/api/issues",new{projectId=project,summary="Collaboration",assigneeUserId=assignee});Assert.Equal(HttpStatusCode.Created,r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }
    [Fact]
    public async Task ConcurrentCreationRetriesReturnOneIssueAndDifferentPayloadConflicts()
    {
        var project=await Project();var key=Guid.NewGuid().ToString();var body=new{projectId=project,summary="Replay"};
        var responses=await Task.WhenAll(Send("POST","/api/issues",body,key:key),Send("POST","/api/issues",body,key:key));
        try
        {
            Assert.All(responses,r=>Assert.Equal(HttpStatusCode.Created,r.StatusCode));
            var a=await responses[0].Content.ReadFromJsonAsync<JsonElement>();var b=await responses[1].Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(a.GetProperty("id").GetInt64(),b.GetProperty("id").GetInt64());
            Assert.Single(responses,r=>r.Headers.Contains("Idempotency-Replayed"));
        }finally{foreach(var response in responses)response.Dispose();}
        using var conflict=await Send("POST","/api/issues",new{projectId=project,summary="Different"},key:key);Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();Assert.Equal(1,await db.Issues.CountAsync(i=>i.ProjectId==project));
    }
    [Fact]
    public async Task CommentAndAssignmentRetriesDoNotDuplicateWrites()
    {
        var project=await Project();var issue=await Issue(project,101);var key=Guid.NewGuid().ToString();
        using var one=await Send("POST",$"/api/issues/{issue}/comments",new{body="Once"},key:key);
        using var two=await Send("POST",$"/api/issues/{issue}/comments",new{body="Once"},key:key);
        Assert.Equal(HttpStatusCode.OK,one.StatusCode);Assert.Equal(HttpStatusCode.OK,two.StatusCode);
        using var read=await Send("GET",$"/api/issues/{issue}");var tag=read.Headers.ETag!.Tag;
        var assignKey=Guid.NewGuid().ToString();
        using var a=await Send("PUT",$"/api/issues/{issue}/assignee",new{assigneeUserId=100},key:assignKey,tag:tag);
        using var b=await Send("PUT",$"/api/issues/{issue}/assignee",new{assigneeUserId=100},key:assignKey,tag:tag);
        Assert.Equal(HttpStatusCode.OK,a.StatusCode);Assert.Equal(HttpStatusCode.OK,b.StatusCode);Assert.True(b.Headers.Contains("Idempotency-Replayed"));
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();Assert.Equal(1,await db.IssueComments.CountAsync(c=>c.IssueId==issue));
    }
    [Fact]
    public async Task WorkflowRulesApplyToBothStatusWritePathsAndVersionGuardConfiguration()
    {
        var project=await Project();var issue=await Issue(project);
        var definition=new{initialStatus="TODO",statuses=new[]{"TODO","IN_PROGRESS","DONE"},transitions=new[]{new{from="TODO",to="IN_PROGRESS"},new{from="IN_PROGRESS",to="DONE"}}};
        using var save=await Send("PUT",$"/api/projects/{project}/workflow",new{definition,expectedVersion=0});Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        using var stale=await Send("PUT",$"/api/projects/{project}/workflow",new{definition,expectedVersion=0});Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);
        foreach(var path in new[]{$"/api/issues/{issue}",$"/api/issues/{issue}/transition"})
        {using var invalid=await Send("PUT",path,new{statusCode="DONE",confirmSkip=true});Assert.Equal(HttpStatusCode.Conflict,invalid.StatusCode);}
        using var allowed=await Send("PUT",$"/api/issues/{issue}/transition",new{statusCode="IN_PROGRESS"});Assert.Equal(HttpStatusCode.OK,allowed.StatusCode);
        using var denied=await Send("PUT",$"/api/projects/{project}/workflow",new{definition,expectedVersion=0},101,"DEVELOPER");Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
    }
    [Fact]
    public async Task BoardCardsAndActivityAreFilteredByIssueScopeAndProjectionDeduplicates()
    {
        var project=await Project();var mine=await Issue(project,101);var other=await Issue(project,102);
        using var created=await Send("POST","/api/boards",new{projectId=project,name="Team",boardType="KANBAN"});Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var board=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        using var view=await Send("GET",$"/api/boards/{board}",user:101,role:"DEVELOPER");Assert.Equal(HttpStatusCode.OK,view.StatusCode);
        var cards=(await view.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToArray();Assert.Equal(mine,Assert.Single(cards).GetProperty("id").GetInt64());
        using var scope=fixture.App.Services.CreateScope();var projection=scope.ServiceProvider.GetRequiredService<ActivityProjection>();
        var e=new IssueAuditEvent{EventId=Guid.NewGuid(),TenantId=1,EntityType="ISSUE",EntityId=mine,EntityKey="TEST",EventType="ISSUE_UPDATED",PerformedByUserId=100,OccurredAt=DateTimeOffset.UtcNow};
        await projection.ApplyAsync(e,default);await projection.ApplyAsync(e,default);
        using var history=await Send("GET",$"/api/issues/{mine}/activity",user:101,role:"DEVELOPER");Assert.Equal(1,(await history.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
        using var forbidden=await Send("GET",$"/api/issues/{other}/activity",user:101,role:"DEVELOPER");Assert.Equal(HttpStatusCode.NotFound,forbidden.StatusCode);
        using var adminFeed=await Send("GET","/api/activity",user:101,role:"DEVELOPER");Assert.Equal(HttpStatusCode.Forbidden,adminFeed.StatusCode);
    }
    [Fact]
    public async Task WatchersAndMentionsNotifyOnlyEligiblePeople()
    {
        var project=await Project();var issue=await Issue(project,101);
        using var watch=await Send("PUT",$"/api/issues/{issue}/watchers/me",user:101,role:"DEVELOPER");Assert.Equal(HttpStatusCode.NoContent,watch.StatusCode);
        using var update=await Send("PUT",$"/api/issues/{issue}",new{summary="Watched change"});Assert.Equal(HttpStatusCode.OK,update.StatusCode);
        using var mention=await Send("POST",$"/api/issues/{issue}/comments",new{body="Please check @[101]",mentionedUserIds=new[]{101}});Assert.Equal(HttpStatusCode.OK,mention.StatusCode);
        using var bad=await Send("POST",$"/api/issues/{issue}/comments",new{body="Wrong",mentionedUserIds=new[]{200}});Assert.Equal(HttpStatusCode.BadRequest,bad.StatusCode);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        Assert.True(await db.Notifications.AnyAsync(n=>n.IssueId==issue&&n.RecipientUserId==101&&n.Kind=="ISSUE_MENTIONED"));
        Assert.True(await db.Notifications.AnyAsync(n=>n.IssueId==issue&&n.RecipientUserId==101&&n.Kind=="ISSUE_UPDATED"));
        using var forbidden=await Send("PUT",$"/api/issues/{issue}/watchers/me",user:102,role:"DEVELOPER");Assert.Equal(HttpStatusCode.NotFound,forbidden.StatusCode);
    }
    [Fact]
    public async Task ReminderRunsAreDeduplicatedAndCompletedIssuesAreExcluded()
    {
        var project=await Project();var issue=await Issue(project,101);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE issues SET due_date=CURRENT_DATE-1 WHERE id={issue}");
        await CollaborationJobs.RunOnce(db,default);await CollaborationJobs.RunOnce(db,default);
        Assert.Equal(1,await db.Notifications.CountAsync(n=>n.IssueId==issue&&n.Kind=="ISSUE_OVERDUE"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE issues SET status_code='DONE' WHERE id={issue}");
        await CollaborationJobs.RunOnce(db,default);Assert.Equal(1,await db.Notifications.CountAsync(n=>n.IssueId==issue&&n.Kind=="ISSUE_OVERDUE"));
    }
    [Fact]
    public void PoisonEventsAreRejectedAndRetryBackoffIsBounded()
    {
        Assert.Throws<JsonException>(()=>ReliableAuditConsumer.Parse("{}"));Assert.Throws<JsonException>(()=>ReliableAuditConsumer.Parse("bad-json"));
        Assert.Equal(TimeSpan.FromSeconds(1),ReliableAuditConsumer.RetryDelay(1));Assert.Equal(TimeSpan.FromSeconds(16),ReliableAuditConsumer.RetryDelay(20));
    }
}
