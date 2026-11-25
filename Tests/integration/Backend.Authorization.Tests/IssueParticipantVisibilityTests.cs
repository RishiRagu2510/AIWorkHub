using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;

public sealed class IssueParticipantVisibilityTests(SecurityFixture fixture):IClassFixture<SecurityFixture>
{
 async Task<HttpResponseMessage> Send(string method,string path,long user,object? body=null) {
  using var request=new HttpRequestMessage(new HttpMethod(method),path);
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,1,user==100?"ADMIN":"DEVELOPER"));
  if(body!=null)request.Content=JsonContent.Create(body);
  await VersionedTestRequests.AddCurrentVersionAsync(fixture.Client,request);
  return await fixture.Client.SendAsync(request);
 }
 static async Task<JsonElement> Json(HttpResponseMessage response) {Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());return await response.Content.ReadFromJsonAsync<JsonElement>();}
 [Fact] public async Task OnlyCreatorAndCurrentAssigneeCanSeeDeveloperIssueAfterReassignment() {
  using(var serviceScope=fixture.App.Services.CreateScope()) {
   var identity=serviceScope.ServiceProvider.GetRequiredService<IdentityTenant.Infrastructure.Persistence.IdentityTenantDbContext>();
   identity.UserRoles.Add(new IdentityTenant.Domain.Entities.UserRole(1,104,2));
   await identity.SaveChangesAsync();
   var db=serviceScope.ServiceProvider.GetRequiredService<JiraDbContext>();
   await db.Database.ExecuteSqlRawAsync("INSERT INTO project_members(tenant_id,project_id,user_id,role,created_at) VALUES(1,1,101,'DEVELOPER',now()),(1,1,102,'DEVELOPER',now()),(1,1,104,'DEVELOPER',now()) ON CONFLICT DO NOTHING");
  }
  var created=await Json(await Send("POST","/api/issues",101,new{projectId=1,summary="Participant privacy regression",assigneeUserId=102}));
  var id=created.GetProperty("id").GetInt64();
  Assert.Equal(101,created.GetProperty("reporterUserId").GetInt64());
  async Task Verify(long user,bool visible) {
   Assert.Equal(visible?HttpStatusCode.OK:HttpStatusCode.NotFound,(await Send("GET",$"/api/issues/{id}",user)).StatusCode);
   Assert.Equal(visible?HttpStatusCode.OK:HttpStatusCode.NotFound,(await Send("GET",$"/api/issues/{id}/comments",user)).StatusCode);
   Assert.Equal(visible?HttpStatusCode.OK:HttpStatusCode.NotFound,(await Send("GET",$"/api/issues/{id}/attachments",user)).StatusCode);
   var search=await Json(await Send("GET","/api/issues/search?pageSize=100",user));
   Assert.Equal(visible,search.GetProperty("items").EnumerateArray().Any(x=>x.GetProperty("id").GetInt64()==id));
   if(!visible) {
    var notifications=await Json(await Send("GET","/api/notifications?pageSize=100",user));
    Assert.DoesNotContain(notifications.GetProperty("items").EnumerateArray(),x=>x.GetProperty("issueId").GetInt64()==id);
   }
  }
  await Verify(101,true);await Verify(102,true);await Verify(104,false);await Verify(100,true);
  await Json(await Send("PUT",$"/api/issues/{id}/assignee",101,new{assigneeUserId=104}));
  await Verify(101,true);await Verify(102,false);await Verify(104,true);
  await Json(await Send("PUT",$"/api/issues/{id}/assignee",101,new{clearAssignee=true}));
  await Verify(101,true);await Verify(102,false);await Verify(104,false);

  
  var multi = await Json(await Send("POST","/api/issues",101,new{projectId=1,summary="Multi assignee test",assigneeUserIds=new long[]{102,104}}));
  var multiId = multi.GetProperty("id").GetInt64();
  Assert.Equal(102, multi.GetProperty("assigneeUserId").GetInt64());
  var multiAssignees = multi.GetProperty("assigneeUserIds").EnumerateArray().Select(x=>x.GetInt64()).ToArray();
  Assert.Contains(102, multiAssignees);
  Assert.Contains(104, multiAssignees);

  
  Assert.Equal(HttpStatusCode.OK, (await Send("GET",$"/api/issues/{multiId}", 102)).StatusCode);
  Assert.Equal(HttpStatusCode.OK, (await Send("GET",$"/api/issues/{multiId}", 104)).StatusCode);
 }
}
