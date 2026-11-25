using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityTenant.Contracts;
using IdentityTenant.Infrastructure.Persistence;
using IdentityTenant.Domain.Careers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;
public sealed class EmployeeOnboardingTests(SecurityFixture fixture):IClassFixture<SecurityFixture>
{
 async Task<HttpResponseMessage> Send(string method,string path,object? body=null,long user=100,long tenant=1,string? role=null,string[]? permissions=null,string? key=null,string? approval=null) {
  using var request=new HttpRequestMessage(new HttpMethod(method),path);
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,tenant,role??(path=="/api/issues/employee-requests"?"TALENT_ACQUISITION":"ADMIN"),permissions??SecurityFixture.Permissions));
  if(body is not null)request.Content=JsonContent.Create(body);
  if(key is not null)request.Headers.Add("Idempotency-Key",key);
  if(approval is not null)request.Headers.Add(Authentication.OnboardingApprovalSigner.Header,approval);
  return await fixture.Client.SendAsync(request);
 }
 static async Task<JsonElement> Read(HttpResponseMessage response){var text=await response.Content.ReadAsStringAsync();Assert.True(response.IsSuccessStatusCode,text);return JsonSerializer.Deserialize<JsonElement>(text);}
 [Fact]
 public async Task CompleteRequestRequiresValidManagerApprovalAndVerifiedIdentityAndIsRetrySafe() {
  using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
  var jiraDb=scope.ServiceProvider.GetRequiredService<Jira.Infrastructure.Persistence.JiraDbContext>();
  await jiraDb.Database.ExecuteSqlRawAsync("INSERT INTO project_members(tenant_id,project_id,user_id,role,created_at) VALUES(1,1,100,'DEVELOPER',CURRENT_TIMESTAMP)");
  var hrRole=new IdentityTenant.Domain.Entities.Role(1,"HR","HR");db.Roles.Add(hrRole);await db.SaveChangesAsync();
  db.UserRoles.AddRange(new IdentityTenant.Domain.Entities.UserRole(1,103,hrRole.Id),new IdentityTenant.Domain.Entities.UserRole(1,104,hrRole.Id));
  foreach(var permission in await db.Permissions.Where(p=>p.Code=="EMPLOYEE_CREATE"||p.Code=="ISSUE_VIEW").ToListAsync())db.RolePermissions.Add(new IdentityTenant.Domain.Entities.RolePermission(hrRole.Id,permission.Id));
  await db.SaveChangesAsync();
  await jiraDb.Database.ExecuteSqlRawAsync("INSERT INTO project_members(tenant_id,project_id,user_id,role,created_at) VALUES(1,1,103,'DEVELOPER',CURRENT_TIMESTAMP)");
  var level=new CareerLevel{CareerProfile=new(){Code="ONBOARD",Name="Onboarding career"},Grade=new(){Code="T1",Name="Junior"},CareerTitle="Junior Engineer"};db.CareerLevels.Add(level);await db.SaveChangesAsync();
  var manager=await Read(await Send("POST","/api/employees",new EmployeeRequest(100,"MANAGER100","Manager","One",new DateOnly(2025,1,1))));
  var otherManager=await Read(await Send("POST","/api/employees",new EmployeeRequest(200,"MANAGER200","Other","Tenant",new DateOnly(2025,1,1)),200,2));
  var draft=new EmployeeRequest(0,"","New","Employee",new DateOnly(2026,10,1),ManagerEmployeeId:manager.GetProperty("id").GetInt64(),CareerProfileId:level.CareerProfileId,CareerLevelId:level.Id,CatalogGradeId:level.GradeId,CatalogDepartmentId:level.CareerProfileId);
  EmployeeOnboardingRequest Proposal(EmployeeRequest value)=>new("user101@company.com",JsonSerializer.SerializeToElement(value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
  object Issue(EmployeeRequest value)=>new{issue=new{projectId=1,summary="Add new employee",issueType="TASK",priority="HIGH",statusCode="TODO"},proposal=Proposal(value)};
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST","/api/issues/employee-requests",Issue(draft with{ManagerEmployeeId=otherManager.GetProperty("id").GetInt64()}))).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST","/api/issues/employee-requests",Issue(draft with{FirstName=""}))).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("POST","/api/issues/employee-requests",Issue(draft),permissions:[])).StatusCode);
  var worker=await Read(await Send("POST","/api/employees",new EmployeeRequest(102,"WORKER102","Worker","Two",new DateOnly(2025,1,1))));
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST","/api/issues/employee-requests",Issue(draft with{ManagerEmployeeId=worker.GetProperty("id").GetInt64()}))).StatusCode);
  await db.Set<IdentityTenant.Domain.Employees.Employee>().Where(e=>e.Id==draft.ManagerEmployeeId).ExecuteUpdateAsync(u=>u.SetProperty(e=>e.EmploymentStatus,"INACTIVE"));
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST","/api/issues/employee-requests",Issue(draft))).StatusCode);
  await db.Set<IdentityTenant.Domain.Employees.Employee>().Where(e=>e.Id==draft.ManagerEmployeeId).ExecuteUpdateAsync(u=>u.SetProperty(e=>e.EmploymentStatus,"ACTIVE"));
  foreach(var deniedRole in new[]{"ADMIN","HR","DEVELOPER","MANAGER"})
   Assert.Equal(HttpStatusCode.Forbidden,(await Send("POST","/api/issues/employee-requests",Issue(draft),role:deniedRole)).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("GET","/api/employees/onboarding/options",role:"HR")).StatusCode);
  Assert.Equal(HttpStatusCode.OK,(await Send("GET","/api/employees/onboarding/options",role:"TALENT_ACQUISITION")).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await Send("POST","/api/issues/employee-requests",Issue(draft),user:104)).StatusCode);
  var created=await Read(await Send("POST","/api/issues/employee-requests",Issue(draft),key:"onboard-create"));var id=created.GetProperty("id").GetInt64();
  Assert.Equal(id,(await Read(await Send("POST","/api/issues/employee-requests",Issue(draft),key:"onboard-create"))).GetProperty("id").GetInt64());
  var requestedNotices=await jiraDb.Notifications.Where(n=>n.IssueId==id&&n.Kind=="EMPLOYEE_ONBOARDING_REQUESTED").ToListAsync();
  Assert.Equal(new long[]{100,103},requestedNotices.Select(n=>n.RecipientUserId).Order().ToArray());
  Assert.False(await jiraDb.Notifications.AnyAsync(n=>n.IssueId==id&&n.Kind=="ISSUE_ASSIGNED"));
  var hrInbox=await Read(await Send("GET","/api/notifications",user:103,role:"HR"));
  Assert.Contains(hrInbox.GetProperty("items").EnumerateArray(),n=>n.GetProperty("kind").GetString()=="EMPLOYEE_ONBOARDING_REQUESTED");
  await jiraDb.Database.ExecuteSqlRawAsync("DELETE FROM project_members WHERE tenant_id=1 AND project_id=1 AND user_id=103");
  Assert.DoesNotContain((await Read(await Send("GET","/api/notifications",user:103,role:"HR"))).GetProperty("items").EnumerateArray(),n=>n.GetProperty("issueId").GetInt64()==id);
  var path=$"/api/issues/{id}/employee-request";
  var submitted=await Read(await Send("GET",path));
  Assert.Equal("SUBMITTED",submitted.GetProperty("status").GetString());
  
  string[] statusKeys=["data","employeeId","isEmployeeRequest","issueId","requestedAt","requestedBy","reviewedAt","reviewedBy","reviewNote","revision","status"];
  Assert.Equal(statusKeys.Order(StringComparer.Ordinal),submitted.EnumerateObject().Select(p=>p.Name).Order(StringComparer.Ordinal));
  Assert.True(submitted.GetProperty("isEmployeeRequest").GetBoolean());
  Assert.Equal(id,submitted.GetProperty("issueId").GetInt64());
  Assert.Equal(1,submitted.GetProperty("revision").GetInt32());
  Assert.Equal(JsonValueKind.Null,submitted.GetProperty("employeeId").ValueKind);
  Assert.Equal(JsonValueKind.Null,submitted.GetProperty("reviewedBy").ValueKind);
  Assert.Equal(JsonValueKind.Object,submitted.GetProperty("data").GetProperty("proposal").ValueKind);
  var plainIssue=await Read(await Send("POST","/api/issues",new{projectId=1,summary="Not an onboarding request"}));
  var plain=await Read(await Send("GET",$"/api/issues/{plainIssue.GetProperty("id").GetInt64()}/employee-request"));
  Assert.Equal(new[]{"isEmployeeRequest"},plain.EnumerateObject().Select(p=>p.Name).ToArray());
  Assert.False(plain.GetProperty("isEmployeeRequest").GetBoolean());
  Assert.Equal(HttpStatusCode.NotFound,(await Send("GET","/api/issues/999999/employee-request")).StatusCode);
  var badDecision=await Send("POST",path+"/review",new{revision=1,decision="MAYBE"});
  Assert.Equal(HttpStatusCode.BadRequest,badDecision.StatusCode);
  Assert.Equal("Choose approve or reject.",(await badDecision.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
  Assert.Equal(HttpStatusCode.NotFound,(await Send("GET",path,user:200,tenant:2)).StatusCode);
  var final=draft with{UserId=101,EmployeeCode="EMP101",PreferredName="Reviewed by HR"};
  Assert.Equal(HttpStatusCode.Conflict,(await Send("POST",path+"/complete",final)).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("POST",path+"/review",new{revision=1,decision="APPROVED"},user:101,role:"DEVELOPER")).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await Send("POST",path+"/review",new{revision=99,decision="APPROVED"})).StatusCode);
  var approved=await Read(await Send("POST",path+"/review",new{revision=1,decision="APPROVED",note="Ready"}));
  Assert.Equal(statusKeys.Order(StringComparer.Ordinal),approved.EnumerateObject().Select(p=>p.Name).Order(StringComparer.Ordinal));
  Assert.Equal("APPROVED",approved.GetProperty("status").GetString());
  Assert.Equal("Ready",approved.GetProperty("reviewNote").GetString());
  Assert.Equal(100,approved.GetProperty("reviewedBy").GetInt64());
  Assert.Equal(2,approved.GetProperty("revision").GetInt32());
  
  var completed=await Read(await Send("POST",path+"/complete",final with{UserId=102}));var employeeId=completed.GetProperty("employeeId").GetInt64();
  Assert.Equal("COMPLETED",completed.GetProperty("status").GetString());
  Assert.Equal(statusKeys.Order(StringComparer.Ordinal),completed.EnumerateObject().Select(p=>p.Name).Order(StringComparer.Ordinal));
  Assert.Equal(3,completed.GetProperty("revision").GetInt32());
  Assert.Equal(employeeId,(await Read(await Send("POST",path+"/complete",final))).GetProperty("employeeId").GetInt64());
  
  var signer=scope.ServiceProvider.GetRequiredService<Authentication.OnboardingApprovalSigner>();
  Assert.Equal(employeeId,(await Read(await Send("POST",$"/api/employees/from-onboarding/{id}",final,approval:signer.Sign(1,id,"user101@company.com",null,DateTimeOffset.UtcNow)))).GetProperty("id").GetInt64());
  var employee=await Read(await Send("GET",$"/api/employees/{employeeId}"));
  Assert.Equal(101,employee.GetProperty("userId").GetInt64());
  Assert.Equal(draft.ManagerEmployeeId,employee.GetProperty("managerEmployeeId").GetInt64());
  Assert.Equal("Reviewed by HR",employee.GetProperty("preferredName").GetString());
  var rejectedInput=new{issue=new{projectId=1,summary="Rejected hire"},proposal=new EmployeeOnboardingRequest("new@company.com",JsonSerializer.SerializeToElement(draft,new JsonSerializerOptions(JsonSerializerDefaults.Web)),"Another hire")};
  var rejectedId=(await Read(await Send("POST","/api/issues/employee-requests",rejectedInput))).GetProperty("id").GetInt64();
  await Read(await Send("POST",$"/api/issues/{rejectedId}/employee-request/review",new{revision=1,decision="REJECTED"}));
  Assert.Equal(HttpStatusCode.Conflict,(await Send("POST",$"/api/issues/{rejectedId}/employee-request/complete",final)).StatusCode);
  var jira=scope.ServiceProvider.GetRequiredService<Jira.Infrastructure.Persistence.JiraDbContext>();
  Assert.True(await jira.OutboxMessages.AnyAsync(e=>e.EventType=="EMPLOYEE_REQUEST_COMPLETED"));
  Assert.Equal(1,await jira.Notifications.CountAsync(n=>n.IssueId==id&&n.RecipientUserId==100&&n.Kind=="EMPLOYEE_ONBOARDING_COMPLETED"));
  Assert.False(await jira.Notifications.AnyAsync(n=>n.IssueId==rejectedId&&n.Kind=="EMPLOYEE_ONBOARDING_COMPLETED"));
  var audit=await jira.OutboxMessages.Where(e=>e.EventType=="EMPLOYEE_REQUEST_COMPLETED").SingleAsync();
  Assert.Equal(created.GetProperty("issueKey").GetString(),JsonSerializer.Deserialize<Messaging.Events.IssueAuditEvent>(audit.Payload)!.EntityKey);
  Assert.Equal(1,await db.Set<IdentityTenant.Domain.Employees.Employee>().CountAsync(e=>e.UserId==101));
 }
}
