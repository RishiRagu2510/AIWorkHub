using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Jira.Contracts.Issues;
using Jira.Contracts.Timesheets;
using Jira.Domain.Entities;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;
public sealed class PlanningApiTests(SecurityFixture fixture):IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method,string path,object? body=null,uint? version=null,long tenant=1,long user=100,string role="ADMIN",string[]? permissions=null) {
        using var request=new HttpRequestMessage(new HttpMethod(method),path);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,tenant,role,permissions??SecurityFixture.Permissions));
        if(body is not null)request.Content=JsonContent.Create(body);
        if(version.HasValue)request.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");
        return await fixture.Client.SendAsync(request);
    }
    [Fact] public async Task MetadataEstimatesAndTimeRangesRemainScopedAndAudited() {
        using var component=await Send("POST","/api/projects/1/components",new{name="Planning component"});Assert.Equal(HttpStatusCode.Created,component.StatusCode);var c=(await component.Content.ReadFromJsonAsync<ProjectComponent>())!;
        using var release=await Send("POST","/api/projects/1/versions",new{name="planning-1.0"});Assert.Equal(HttpStatusCode.Created,release.StatusCode);var r=(await release.Content.ReadFromJsonAsync<ProjectRelease>())!;
        using var edited=await Send("PUT",$"/api/projects/1/versions/{r.Id}",new{name="planning-1.1",status="RELEASED"},r.Version);Assert.Equal(HttpStatusCode.OK,edited.StatusCode);
        using var staleVersion=await Send("PUT",$"/api/projects/1/versions/{r.Id}",new{name="planning-1.2"},r.Version);Assert.Equal(HttpStatusCode.PreconditionFailed,staleVersion.StatusCode);
        using var otherRelease=await Send("POST","/api/projects/3/versions",new{name="foreign-version"},tenant:2,user:200);var foreignVersion=(await otherRelease.Content.ReadFromJsonAsync<ProjectRelease>())!;
        using var rejected=await Send("POST","/api/issues",new{projectId=1,summary="Cross tenant version",fixVersionIds=new[]{foreignVersion.Id}});Assert.Equal(HttpStatusCode.NotFound,rejected.StatusCode);
        using var created=await Send("POST","/api/issues",new{projectId=1,summary="Planning integration",issueType="TASK",labels=new[]{"API","api"},fixVersionIds=new[]{r.Id},componentId=c.Id,originalEstimateMinutes=120,remainingEstimateMinutes=120});
        Assert.True(created.StatusCode==HttpStatusCode.Created,await created.Content.ReadAsStringAsync());var issue=(await created.Content.ReadFromJsonAsync<IssueDto>())!;Assert.Equal(new[]{"api"},issue.Labels);Assert.Equal(120,issue.OriginalEstimateMinutes);
        using var log=await Send("POST","/api/worklogs",new{issueId=issue.Id,workDate="2025-09-02",startTime="09:00",endTime="10:30",durationMinutes=1,billable=true});Assert.Equal(HttpStatusCode.Created,log.StatusCode);var w=(await log.Content.ReadFromJsonAsync<WorkLogDto>())!;Assert.Equal(90,w.DurationMinutes);Assert.Equal("DRAFT",w.Status);
        using var updated=await Send("GET",$"/api/issues/{issue.Id}");var after=(await updated.Content.ReadFromJsonAsync<IssueDto>())!;Assert.Equal(90,after.LoggedTimeMinutes);Assert.Equal(30,after.RemainingEstimateMinutes);
        using var invalid=await Send("PUT",$"/api/worklogs/{w.Id}",new{issueId=issue.Id,workDate="2025-09-02",startTime="10:00",endTime="09:00",durationMinutes=60},w.Version);Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        using var wrongTenant=await Send("GET",$"/api/worklogs/{w.Id}",tenant:2,user:200);Assert.Equal(HttpStatusCode.NotFound,wrongTenant.StatusCode);
        using var deleted=await Send("DELETE",$"/api/worklogs/{w.Id}",version:w.Version);Assert.Equal(HttpStatusCode.NoContent,deleted.StatusCode);
        using var refreshed=await Send("GET",$"/api/issues/{issue.Id}");Assert.Equal(0,(await refreshed.Content.ReadFromJsonAsync<IssueDto>())!.LoggedTimeMinutes);
        using var filter=await Send("GET",$"/api/issues/search?Label=api&FixVersionId={r.Id}");var search=await filter.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(1,search.GetProperty("totalCount").GetInt32());
        using var denied=await Send("POST","/api/projects/1/components",new{name="No permission"},permissions:["PROJECT_VIEW"]);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var foreign=await Send("GET","/api/projects/1/versions",tenant:2,user:200);Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
    }
    [Fact] public async Task SprintLifecycleEnforcesVersionsProjectScopeAndReturnsUnfinishedWork() {
        using var boardResponse=await Send("POST","/api/boards",new{projectId=1,name="Planning Scrum",boardType="SCRUM"});Assert.Equal(HttpStatusCode.Created,boardResponse.StatusCode);var board=(await boardResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        using var created=await Send("POST",$"/api/boards/{board}/sprints",new{name="Iteration 1",startDate="2026-10-01",endDate="2026-10-14"});Assert.Equal(HttpStatusCode.Created,created.StatusCode);var sprint=(await created.Content.ReadFromJsonAsync<Sprint>())!;
        using var issueResponse=await Send("POST","/api/issues",new{projectId=1,summary="Sprint work",issueType="TASK"});var issue=(await issueResponse.Content.ReadFromJsonAsync<IssueDto>())!;
        using var placed=await Send("PUT",$"/api/boards/{board}/issues/{issue.Id}/placement",new{sprintId=sprint.Id,position=10},issue.Version);Assert.Equal(HttpStatusCode.NoContent,placed.StatusCode);
        using var stale=await Send("PUT",$"/api/boards/{board}/issues/{issue.Id}/placement",new{position=20},issue.Version);Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);
        using var missing=await Send("POST",$"/api/sprints/{sprint.Id}/start");Assert.Equal((HttpStatusCode)428,missing.StatusCode);
        using var started=await Send("POST",$"/api/sprints/{sprint.Id}/start",version:sprint.Version);Assert.Equal(HttpStatusCode.OK,started.StatusCode);sprint=(await started.Content.ReadFromJsonAsync<Sprint>())!;
        using var denied=await Send("POST",$"/api/sprints/{sprint.Id}/complete",version:sprint.Version,user:101,role:"DEVELOPER");Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var foreign=await Send("GET",$"/api/boards/{board}/backlog",tenant:2,user:200);Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
        using var completed=await Send("POST",$"/api/sprints/{sprint.Id}/complete",version:sprint.Version);Assert.Equal(HttpStatusCode.OK,completed.StatusCode);
        using var backlog=await Send("GET",$"/api/boards/{board}/backlog");var page=await backlog.Content.ReadFromJsonAsync<JsonElement>();Assert.Contains(page.GetProperty("items").EnumerateArray(),x=>x.GetProperty("id").GetInt64()==issue.Id);
    }
    [Theory]
    [InlineData("/api/projects/1/components")]
    [InlineData("/api/projects/1/versions")]
    [InlineData("/api/boards/1/backlog")]
    public async Task AnonymousIsRejected(string path){using var response=await fixture.Client.GetAsync(path);Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);}
}
