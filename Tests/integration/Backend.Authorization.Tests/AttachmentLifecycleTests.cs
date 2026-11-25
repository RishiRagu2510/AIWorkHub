using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Jira.Api.Collaboration;
using IdentityTenant.Infrastructure.Persistence;
using IdentityTenant.Domain.Entities;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;
public sealed class FakeAttachmentStorage : IAttachmentStorage {
 public readonly Dictionary<string,byte[]> Objects=[];
 public bool FailRemove;
 public async Task Put(string key,Stream stream,long size,CancellationToken ct){using var bytes=new MemoryStream();await stream.CopyToAsync(bytes,ct);Objects[key]=bytes.ToArray();}
 public Task<byte[]> Get(string key,CancellationToken ct)=>Task.FromResult(Objects[key]);
 public Task Remove(string key,CancellationToken ct){if(FailRemove)throw new IOException();Objects.Remove(key);return Task.CompletedTask;}
 public async IAsyncEnumerable<string> OldObjects([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct){await Task.CompletedTask;foreach(var key in Objects.Keys.ToArray()){ct.ThrowIfCancellationRequested();yield return key;}}
}
public sealed class AttachmentLifecycleTests(SecurityFixture fixture):IClassFixture<SecurityFixture> {
 async Task<HttpResponseMessage> Send(string method,string path,object? body=null,long user=100,long tenant=1,string role="ADMIN",HttpContent? content=null){using var r=new HttpRequestMessage(new HttpMethod(method),path);r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,tenant,role));r.Content=content ?? (body is null?null:JsonContent.Create(body));return await fixture.Client.SendAsync(r);}
 [Fact] public async Task AttachmentsEnforceOwnershipAndRetryOrphanCleanup(){
  using var member=await Send("POST","/api/projects/1/members",new{userId=101});
  using var issueResponse=await Send("POST","/api/issues",new{projectId=1,summary="Attachment lifecycle",assigneeUserId=101});
  Assert.Equal(HttpStatusCode.Created,issueResponse.StatusCode);var issue=(await issueResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
  var form=new MultipartFormDataContent();form.Add(new ByteArrayContent([1,2,3]),"file","example.txt");
  using var upload=await Send("POST",$"/api/issues/{issue}/attachments",content:form);Assert.Equal(HttpStatusCode.OK,upload.StatusCode);
  var file=(await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  using var forbidden=await Send("DELETE",$"/api/issues/{issue}/attachments/{file}",user:101,role:"DEVELOPER");Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);
  using var crossTenant=await Send("GET",$"/api/issues/{issue}/attachments/{file}/download",tenant:2);Assert.Equal(HttpStatusCode.NotFound,crossTenant.StatusCode);
  var storage=(FakeAttachmentStorage)fixture.App.Services.GetRequiredService<IAttachmentStorage>();storage.FailRemove=true;
  using var deleted=await Send("DELETE",$"/api/issues/{issue}/attachments/{file}");Assert.Equal(HttpStatusCode.NoContent,deleted.StatusCode);
  using var hidden=await Send("GET",$"/api/issues/{issue}/attachments/{file}/download");Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);Assert.Single(storage.Objects);
  storage.FailRemove=false;using var scope=fixture.App.Services.CreateScope();await AttachmentCleanup.RunOnce(scope.ServiceProvider.GetRequiredService<JiraDbContext>(),storage,default);Assert.Empty(storage.Objects);
 }
 [Fact] public async Task MentionsAllowAnAuthorizedProjectLeadBeyondIssueParticipants(){
  using(var seed=fixture.App.Services.CreateScope()) {
    var identity=seed.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
    identity.Users.Add(SecurityFixture.Id(new User(1,"lead105@company.com","Project lead"),105));
    var role=SecurityFixture.Id(new Role(1,"PROJECT_MANAGER","Project manager"),99);identity.Roles.Add(role);await identity.SaveChangesAsync();
    identity.UserRoles.Add(new UserRole(1,105,99));await identity.SaveChangesAsync();
  }

  using var response=await Send("POST","/api/issues",new{projectId=2,summary="Mention project lead"});Assert.Equal(HttpStatusCode.Created,response.StatusCode);
  var id=(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
  using var allowed=await Send("POST",$"/api/issues/{id}/comments",new{body="Please review",mentionedUserIds=new[]{105L}});Assert.Equal(HttpStatusCode.OK,allowed.StatusCode);
  using var forbidden=await Send("POST",$"/api/issues/{id}/comments",new{body="Not eligible",mentionedUserIds=new[]{101L}});Assert.Equal(HttpStatusCode.BadRequest,forbidden.StatusCode);
 }
 [Fact] public async Task PreferenceWritesReclaimExpiredDraftsButKeepOtherUsersAndSettings(){
  using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
  for(var i=0;i<100;i++) db.Add(new UserPreference{TenantId=1,UserId=100,Key=$"issue-draft:old{i}",Value="null",UpdatedAt=DateTimeOffset.UtcNow.AddDays(-8)});
  db.Add(new UserPreference{TenantId=1,UserId=101,Key="issue-draft:keep",Value="null",UpdatedAt=DateTimeOffset.UtcNow.AddDays(-8)});await db.SaveChangesAsync();
  using var result=await Send("PUT","/api/preferences/theme",new{value="dark",version=0});Assert.Equal(HttpStatusCode.OK,result.StatusCode);
  Assert.Equal(1,await db.Set<UserPreference>().CountAsync(p=>p.UserId==100));Assert.Equal(1,await db.Set<UserPreference>().CountAsync(p=>p.UserId==101));
 }
}
