using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Audit.Application.Services;
using Jira.Contracts.Timesheets;
using Jira.Infrastructure.Persistence;
using Messaging.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;
public sealed class TimesheetApiTests(SecurityFixture fixture):IClassFixture<SecurityFixture>
{
    private static readonly string[] Own=["ISSUE_VIEW","WORKLOG_VIEW","WORKLOG_CREATE","WORKLOG_UPDATE","WORKLOG_DELETE","TIMESHEET_VIEW"];
    private async Task<HttpResponseMessage> Send(string method,string path,object? body=null,long user=101,long tenant=1,string role="DEVELOPER",uint? version=null,string? key=null,string[]? permissions=null) {
        using var request=new HttpRequestMessage(new HttpMethod(method),path);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,tenant,role,permissions??(role=="ADMIN"?SecurityFixture.Permissions:Own)));
        if(version.HasValue)request.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");
        if(key!=null)request.Headers.TryAddWithoutValidation("Idempotency-Key",key);
        if(body!=null)request.Content=JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }
    private static WorkLogRequest Request(string date="2025-01-07",int minutes=90,bool billable=true,long issue=10)=>new(issue,DateOnly.Parse(date),minutes,"Investigated issue",billable);
    private async Task<WorkLogDto> Create(string date,int minutes=90,bool billable=true,long user=101,long issue=10) {
        using var r=await Send("POST","/api/worklogs",Request(date,minutes,billable,issue),user:user);Assert.True(r.StatusCode==HttpStatusCode.Created,await r.Content.ReadAsStringAsync());
        var dto=(await r.Content.ReadFromJsonAsync<WorkLogDto>())!;Assert.NotNull(r.Headers.ETag);return dto;
    }
    private async Task<TimesheetReport> Report(string from,string to,bool team=false,long user=101,long tenant=1) {
        using var r=await Send("GET",$"/api/timesheets/{(team?"team":"me")}?from={from}&to={to}",user:team?100:user,tenant:tenant,role:team?"ADMIN":"DEVELOPER");
        Assert.Equal(HttpStatusCode.OK,r.StatusCode);return (await r.Content.ReadFromJsonAsync<TimesheetReport>())!;
    }
    [Fact] public async Task OwnCrudDerivesIdentityAndEnforcesVersionAndOwnership() {
        using var created=await Send("POST","/api/worklogs",new{issueId=10,workDate="2025-02-04",durationMinutes=60,billable=true,tenantId=2,userId=200,projectId=3});
        Assert.True(created.StatusCode==HttpStatusCode.Created,await created.Content.ReadAsStringAsync());var w=(await created.Content.ReadFromJsonAsync<WorkLogDto>())!;
        Assert.Equal(101,w.UserId);Assert.Equal(1,w.ProjectId);
        using var denied=await Send("PUT",$"/api/worklogs/{w.Id}",Request("2025-02-04"),user:102,version:w.Version);Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        using var missing=await Send("PUT",$"/api/worklogs/{w.Id}",Request("2025-02-04"));Assert.Equal((HttpStatusCode)428,missing.StatusCode);
        using var edit=await Send("PUT",$"/api/worklogs/{w.Id}",Request("2025-02-04",120),version:w.Version);Assert.Equal(HttpStatusCode.OK,edit.StatusCode);
        var updated=(await edit.Content.ReadFromJsonAsync<WorkLogDto>())!;Assert.Equal(120,updated.DurationMinutes);
        using var stale=await Send("DELETE",$"/api/worklogs/{w.Id}",version:w.Version);Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);
        using var forbidden=await Send("DELETE",$"/api/worklogs/{w.Id}",user:102,version:updated.Version);Assert.Equal(HttpStatusCode.NotFound,forbidden.StatusCode);
        using var delete=await Send("DELETE",$"/api/worklogs/{w.Id}",version:updated.Version);Assert.Equal(HttpStatusCode.NoContent,delete.StatusCode);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        var events=(await db.OutboxMessages.Where(e=>e.EventType.StartsWith("WORKLOG_")).Select(e=>e.Payload).ToListAsync()).Select(p=>JsonSerializer.Deserialize<IssueAuditEvent>(p)!).Where(e=>e.EntityId==w.Id).ToArray();
        Assert.Equal(new[]{"WORKLOG_CREATED","WORKLOG_DELETED","WORKLOG_UPDATED"},events.Select(e=>e.EventType).Order().ToArray());
        Assert.All(events,e=>{Assert.Equal(1,e.TenantId);Assert.Equal(101,e.PerformedByUserId);Assert.Equal("WORKLOG",e.EntityType);});
    }
    [Theory][InlineData(0)][InlineData(-1)][InlineData(1441)] public async Task InvalidDuration(int minutes) {
        using var r=await Send("POST","/api/worklogs",Request(minutes:minutes));Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
    }
    [Theory][InlineData(11)][InlineData(20)][InlineData(99999)] public async Task InaccessibleForeignOrMissingIssue(long issue) {
        using var r=await Send("POST","/api/worklogs",Request(issue:issue));Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);
    }
    [Fact] public async Task InvalidDatesDescriptionAndDailyLimit() {
        using var date=await Send("POST","/api/worklogs",Request("1999-01-01"));Assert.Equal(HttpStatusCode.BadRequest,date.StatusCode);
        using var desc=await Send("POST","/api/worklogs",Request() with{Description=new string('x',4001)});Assert.Equal(HttpStatusCode.BadRequest,desc.StatusCode);
        await Create("2025-03-04",1400);
        using var excess=await Send("POST","/api/worklogs",Request("2025-03-04",60));Assert.Equal(HttpStatusCode.BadRequest,excess.StatusCode);
    }
    [Fact] public async Task AggregatesAndFiltersAreScopedAndPaged() {
        await Create("2025-04-07",150);await Create("2025-04-08",60,false);await Create("2025-04-08",30,true,102,11);
        var daily=await Report("2025-04-07","2025-04-07");Assert.Equal(150,daily.TotalMinutes);
        var weekly=await Report("2025-04-07","2025-04-13");Assert.Equal(210,weekly.TotalMinutes);Assert.Equal(150,weekly.BillableMinutes);Assert.Equal(60,weekly.NonBillableMinutes);Assert.Equal(2,weekly.DailyTotals.Count);
        var monthly=await Report("2025-04-01","2025-04-30");Assert.Equal(210,monthly.TotalMinutes);Assert.Equal(210,Assert.Single(monthly.ProjectTotals).Minutes);
        var team=await Report("2025-04-01","2025-04-30",true);Assert.Equal(240,team.TotalMinutes);Assert.Equal(2,team.UserTotals.Count);
        using var denied=await Send("GET","/api/timesheets/team");Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var filter=await Send("GET","/api/worklogs?from=2025-04-01&to=2025-04-30&billable=false&issueId=10&projectId=1&pageSize=1");
        var page=(await filter.Content.ReadFromJsonAsync<WorkLogPage>())!;Assert.Equal(1,page.TotalCount);Assert.False(Assert.Single(page.Items).Billable);
        using var other=await Send("GET","/api/worklogs?from=2025-04-01&to=2025-04-30&userId=102");Assert.Equal(0,(await other.Content.ReadFromJsonAsync<WorkLogPage>())!.TotalCount);
        using var foreign=await Send("GET","/api/timesheets/team?from=2025-04-01&to=2025-04-30",user:200,tenant:2,role:"ADMIN");Assert.Equal(0,(await foreign.Content.ReadFromJsonAsync<TimesheetReport>())!.TotalMinutes);
        using var bad=await Send("GET","/api/timesheets/me?from=2025-05-01&to=2025-04-01");Assert.Equal(HttpStatusCode.BadRequest,bad.StatusCode);
    }
    [Fact] public async Task SubmitRejectCorrectResubmitApproveLocksDataAndNotifies() {
        var w=await Create("2025-05-06");var draft=Assert.Single((await Report("2025-05-05","2025-05-11")).Periods);
        using var submit=await Send("POST","/api/timesheets/submit",new PeriodAction(draft.Id,100),version:draft.Version);
        Assert.Equal(HttpStatusCode.OK,submit.StatusCode);var submitted=(await submit.Content.ReadFromJsonAsync<PeriodDto>())!;
        using var edit=await Send("PUT",$"/api/worklogs/{w.Id}",Request("2025-05-06"),version:w.Version);Assert.Equal(HttpStatusCode.Conflict,edit.StatusCode);
        using var add=await Send("POST","/api/worklogs",Request("2025-05-07"));Assert.Equal(HttpStatusCode.Conflict,add.StatusCode);
        using var unauthorized=await Send("POST","/api/timesheets/approve",new PeriodAction(draft.Id),version:submitted.Version);Assert.Equal(HttpStatusCode.Forbidden,unauthorized.StatusCode);
        using var reason=await Send("POST","/api/timesheets/reject",new PeriodAction(draft.Id),user:100,role:"ADMIN",version:submitted.Version);Assert.Equal(HttpStatusCode.BadRequest,reason.StatusCode);
        using var reject=await Send("POST","/api/timesheets/reject",new PeriodAction(draft.Id,Reason:"Please describe the investigation"),user:100,role:"ADMIN",version:submitted.Version);Assert.Equal(HttpStatusCode.OK,reject.StatusCode);
        using var corrected=await Send("PUT",$"/api/worklogs/{w.Id}",Request("2025-05-06",120),version:w.Version);Assert.Equal(HttpStatusCode.OK,corrected.StatusCode);
        var updated=(await corrected.Content.ReadFromJsonAsync<WorkLogDto>())!;
        var rejected=Assert.Single((await Report("2025-05-05","2025-05-11")).Periods);
        using var resubmit=await Send("POST","/api/timesheets/submit",new PeriodAction(draft.Id,100),version:rejected.Version);Assert.Equal(HttpStatusCode.OK,resubmit.StatusCode);
        var again=(await resubmit.Content.ReadFromJsonAsync<PeriodDto>())!;
        using var approve=await Send("POST","/api/timesheets/approve",new PeriodAction(draft.Id),user:100,role:"ADMIN",version:again.Version);Assert.Equal(HttpStatusCode.OK,approve.StatusCode);
        using var delete=await Send("DELETE",$"/api/worklogs/{w.Id}",version:updated.Version);Assert.Equal(HttpStatusCode.Conflict,delete.StatusCode);
        using var moved=await Send("PUT",$"/api/worklogs/{w.Id}",Request("2025-06-03"),version:updated.Version);Assert.Equal(HttpStatusCode.Conflict,moved.StatusCode);
        using var inbox=await Send("GET","/api/notifications");Assert.Equal(HttpStatusCode.OK,inbox.StatusCode);
        var notifications=await inbox.Content.ReadFromJsonAsync<JsonElement>();Assert.Contains(notifications.GetProperty("items").EnumerateArray(),n=>n.GetProperty("kind").GetString()=="TIMESHEET_APPROVED");
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        Assert.Equal(2,await db.Notifications.CountAsync(n=>n.TimesheetPeriodId==draft.Id&&n.RecipientUserId==100));
        Assert.Equal("Please describe the investigation",(await db.Notifications.SingleAsync(n=>n.TimesheetPeriodId==draft.Id&&n.Kind=="TIMESHEET_REJECTED")).Reason);
        var review=(await Report("2025-05-05","2025-05-11",true)).Periods.Single(p=>p.Id==draft.Id);
        Assert.Equal(120,review.BillableMinutes);Assert.Equal(1,Assert.Single(review.Projects!).ProjectId);
        var events=await db.OutboxMessages.Where(e=>e.EventType.StartsWith("TIMESHEET_")).ToListAsync();Assert.Contains(events,e=>e.EventType=="TIMESHEET_REJECTED");
    }
    [Fact] public async Task RetryUsesSameMutationAndAuditDeduplicates() {
        var key=Guid.NewGuid().ToString();using var first=await Send("POST","/api/worklogs",Request("2025-07-01"),key:key);
        var w=(await first.Content.ReadFromJsonAsync<WorkLogDto>())!;
        using var retry=await Send("POST","/api/worklogs",Request("2025-07-01"),key:key);Assert.Equal(HttpStatusCode.Created,retry.StatusCode);Assert.True(retry.Headers.Contains("Idempotency-Replayed"));Assert.Equal(w.Id,(await retry.Content.ReadFromJsonAsync<WorkLogDto>())!.Id);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        var payloads=await db.OutboxMessages.Where(e=>e.EventType=="WORKLOG_CREATED").Select(e=>e.Payload).ToListAsync();
        var e=Assert.Single(payloads.Select(p=>JsonSerializer.Deserialize<IssueAuditEvent>(p)!),e=>e.EntityId==w.Id);
        var audit=scope.ServiceProvider.GetRequiredService<AuditLogService>();
        Assert.True(await audit.RecordAsync(e.EventId,e.EventType,e.TenantId,e.PerformedByUserId,e.EntityType,e.EntityId,e.EntityKey,"JIRA",e.OldValues,e.NewValues,e.Metadata,e.OccurredAt));
        Assert.False(await audit.RecordAsync(e.EventId,e.EventType,e.TenantId,e.PerformedByUserId,e.EntityType,e.EntityId,e.EntityKey,"JIRA",e.OldValues,e.NewValues,e.Metadata,e.OccurredAt));
    }
    [Fact] public async Task SubmitVersusEditCannotApproveAnUnreviewedChange() {
        var w=await Create("2025-08-05");var p=Assert.Single((await Report("2025-08-04","2025-08-10")).Periods);
        var results=await Task.WhenAll(Send("POST","/api/timesheets/submit",new PeriodAction(p.Id,100),version:p.Version),Send("PUT",$"/api/worklogs/{w.Id}",Request("2025-08-05",120),version:w.Version));
        Assert.Single(results,r=>r.IsSuccessStatusCode);Assert.Single(results,r=>r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed);
        foreach(var r in results)r.Dispose();
    }
    [Fact] public async Task ScopeAndReviewPermissionsCannotBeBypassed() {
        var w=await Create("2025-09-09");var period=Assert.Single((await Report("2025-09-08","2025-09-14")).Periods);
        using var hidden=await Send("GET",$"/api/worklogs/{w.Id}",user:102);Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
        using var foreign=await Send("GET",$"/api/worklogs/{w.Id}",user:200,tenant:2,role:"ADMIN");Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
        using var unavailableReviewer=await Send("POST","/api/timesheets/submit",new PeriodAction(period.Id,102),version:period.Version);Assert.Equal(HttpStatusCode.BadRequest,unavailableReviewer.StatusCode);
        using var submit=await Send("POST","/api/timesheets/submit",new PeriodAction(period.Id,100),version:period.Version);Assert.Equal(HttpStatusCode.OK,submit.StatusCode);
        var submitted=(await submit.Content.ReadFromJsonAsync<PeriodDto>())!;
        using var self=await Send("POST","/api/timesheets/approve",new PeriodAction(period.Id),version:submitted.Version,permissions:SecurityFixture.Permissions);Assert.Equal(HttpStatusCode.Conflict,self.StatusCode);
        using var unauthorized=await Send("POST","/api/timesheets/reject",new PeriodAction(period.Id,Reason:"No"),version:submitted.Version);Assert.Equal(HttpStatusCode.Forbidden,unauthorized.StatusCode);
        using var manager=await Send("POST","/api/timesheets/approve",new PeriodAction(period.Id),user:105,role:"PROJECT_MANAGER",version:submitted.Version,permissions:SecurityFixture.Permissions);Assert.Equal(HttpStatusCode.NotFound,manager.StatusCode);
        using var foreignReview=await Send("POST","/api/timesheets/reject",new PeriodAction(period.Id,Reason:"No"),user:200,tenant:2,role:"ADMIN",version:submitted.Version);Assert.Equal(HttpStatusCode.NotFound,foreignReview.StatusCode);
        using var scoped=await Send("GET","/api/timesheets/team?from=2025-09-08&to=2025-09-14",user:105,role:"PROJECT_MANAGER",permissions:SecurityFixture.Permissions);Assert.Equal(0,(await scoped.Content.ReadFromJsonAsync<TimesheetReport>())!.TotalMinutes);
        using var noAuth=await fixture.Client.GetAsync("/api/worklogs");Assert.Equal(HttpStatusCode.Unauthorized,noAuth.StatusCode);
    }
    [Fact] public async Task ConcurrentReviewsAndActionRetriesProduceOneDecision() {
        await Create("2025-10-07");var p=Assert.Single((await Report("2025-10-06","2025-10-12")).Periods);
        var key=Guid.NewGuid().ToString();using var submit=await Send("POST","/api/timesheets/submit",new PeriodAction(p.Id,100),version:p.Version,key:key);
        Assert.Equal(HttpStatusCode.OK,submit.StatusCode);var submitted=(await submit.Content.ReadFromJsonAsync<PeriodDto>())!;
        using var replay=await Send("POST","/api/timesheets/submit",new PeriodAction(p.Id,100),version:p.Version,key:key);Assert.Equal(HttpStatusCode.OK,replay.StatusCode);Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        var results=await Task.WhenAll(Send("POST","/api/timesheets/approve",new PeriodAction(p.Id),user:100,role:"ADMIN",version:submitted.Version),Send("POST","/api/timesheets/reject",new PeriodAction(p.Id,Reason:"Revise"),user:100,role:"ADMIN",version:submitted.Version));
        Assert.Single(results,r=>r.IsSuccessStatusCode);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.PreconditionFailed);foreach(var r in results)r.Dispose();
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        Assert.Equal(1,await db.Notifications.CountAsync(n=>n.TimesheetPeriodId==p.Id&&n.Kind=="TIMESHEET_SUBMITTED"));
        Assert.Equal(1,await db.Notifications.CountAsync(n=>n.TimesheetPeriodId==p.Id&&n.RecipientUserId==101));
    }
    [Fact] public async Task ProjectManagerCanReviewOnlyTheProjectsTheyLead() {
        await Create("2025-11-04",60,true,102,12);
        var p=Assert.Single((await Report("2025-11-03","2025-11-09",user:102)).Periods);
        using var submit=await Send("POST","/api/timesheets/submit",new PeriodAction(p.Id,100),user:102,version:p.Version);
        var submitted=(await submit.Content.ReadFromJsonAsync<PeriodDto>())!;
        using var scoped=await Send("GET","/api/timesheets/team?from=2025-11-03&to=2025-11-09",user:105,role:"PROJECT_MANAGER",permissions:SecurityFixture.Permissions);
        Assert.Equal(60,(await scoped.Content.ReadFromJsonAsync<TimesheetReport>())!.TotalMinutes);
        using var approve=await Send("POST","/api/timesheets/approve",new PeriodAction(p.Id),user:105,role:"PROJECT_MANAGER",permissions:SecurityFixture.Permissions,version:submitted.Version);
        Assert.Equal(HttpStatusCode.OK,approve.StatusCode);
    }

    [Fact] public async Task HierarchySeparatesUserProjectsAndHidesInaccessibleAncestors() {
        long epicId, storyId;
        using (var scope=fixture.App.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
            var epic=new Jira.Domain.Entities.Issue(1,1,Jira.Domain.Enums.IssueType.Epic,"FLOW-E","Private epic",null,"TODO","MEDIUM",102);
            db.Issues.Add(epic);await db.SaveChangesAsync();epicId=epic.Id;
            var story=new Jira.Domain.Entities.Issue(1,1,Jira.Domain.Enums.IssueType.Story,"FLOW-S","Accessible story",null,"TODO","MEDIUM",101);
            story.SetParentIssue(epic.Id);db.Issues.Add(story);await db.SaveChangesAsync();storyId=story.Id;
        }
        await Create("2025-12-02",90,true,101,storyId);
        await Create("2025-12-02",30,false,102,11);
        await Create("2025-12-02",60,true,102,12);
        const string url="/api/timesheets/hierarchy?from=2025-12-01&to=2025-12-07";
        using var own=await Send("GET",url);Assert.True(own.IsSuccessStatusCode,await own.Content.ReadAsStringAsync());
        var ownGroup=Assert.Single((await own.Content.ReadFromJsonAsync<TimesheetHierarchyPage>())!.Items);
        Assert.Equal(90,ownGroup.Minutes);var node=Assert.Single(ownGroup.Issues);Assert.Equal(storyId,node.IssueId);Assert.Null(node.ParentIssueId);
        using var denied=await Send("GET",url+"&team=true");Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var team=await Send("GET",url+"&team=true",user:100,role:"ADMIN");Assert.True(team.IsSuccessStatusCode,await team.Content.ReadAsStringAsync());
        var all=(await team.Content.ReadFromJsonAsync<TimesheetHierarchyPage>())!;Assert.Equal(3,all.TotalCount);
        Assert.Equal(180,all.Items.Sum(g=>g.Minutes));Assert.Equal(2,all.Items.Count(g=>g.UserId==102));
        var nodes=all.Items.Single(g=>g.UserId==101).Issues;Assert.Equal(2,nodes.Count);Assert.Equal(0,nodes.Single(n=>n.IssueId==epicId).Minutes);Assert.Equal(epicId,nodes.Single(n=>n.IssueId==storyId).ParentIssueId);
        using var paged=await Send("GET",url+"&team=true&pageSize=1&page=2",user:100,role:"ADMIN");Assert.Single((await paged.Content.ReadFromJsonAsync<TimesheetHierarchyPage>())!.Items);
        using var foreign=await Send("GET",url+"&team=true",user:200,tenant:2,role:"ADMIN");Assert.Empty((await foreign.Content.ReadFromJsonAsync<TimesheetHierarchyPage>())!.Items);
    }
}
