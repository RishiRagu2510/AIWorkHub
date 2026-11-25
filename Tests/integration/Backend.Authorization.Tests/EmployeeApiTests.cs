using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;

public sealed class EmployeeApiTests(SecurityFixture fixture):IClassFixture<SecurityFixture>
{
 private async Task<HttpResponseMessage> Send(string method,string path,object? body=null,uint? version=null,long user=100,long tenant=1,string[]? permissions=null) {
  using var request=new HttpRequestMessage(new HttpMethod(method),path);
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,tenant,user==100?"ADMIN":"DEVELOPER",permissions??SecurityFixture.Permissions));
  if(body is not null)request.Content=JsonContent.Create(body);
  if(version.HasValue)request.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");
  return await fixture.Client.SendAsync(request);
 }
 private static async Task<JsonElement> Json(HttpResponseMessage r){Assert.True(r.IsSuccessStatusCode,await r.Content.ReadAsStringAsync());return await r.Content.ReadFromJsonAsync<JsonElement>();}
 [Fact] public async Task UploadedPhotoPersistsAndRequiresAuthorizedEmployeeAndVersion() {
  var employee=await Json(await Send("POST","/api/employees",new{userId=102,employeeCode="PHOTO102",firstName="Photo",lastName="Test",joiningDate="2025-01-01"}));
  var id=employee.GetProperty("id").GetInt64();var version=employee.GetProperty("version").GetUInt32();
  var bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=");
  async Task<HttpResponseMessage> Upload(byte[] data,long user,uint v) {
   using var r=new HttpRequestMessage(HttpMethod.Post,$"/api/employees/{id}/photo");
   r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,1,"DEVELOPER",["EMPLOYEE_VIEW","EMPLOYEE_UPDATE_SELF"]));
   r.Headers.TryAddWithoutValidation("If-Match",$"\"{v}\"");
   r.Content=new MultipartFormDataContent{{new ByteArrayContent(data),"file","photo.png"}};
   return await fixture.Client.SendAsync(r);
  }
  Assert.Equal(HttpStatusCode.Forbidden,(await Upload(bytes,101,version)).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Upload(System.Text.Encoding.UTF8.GetBytes("<svg></svg>"),102,version)).StatusCode);
  var photo=await Json(await Upload(bytes,102,version));var url=photo.GetProperty("profilePhotoUrl").GetString()!;
  var fetched=await Send("GET",url,user:102,permissions:["EMPLOYEE_VIEW"]);
  Assert.Equal(bytes,await fetched.Content.ReadAsByteArrayAsync());
  Assert.Equal("image/png",fetched.Content.Headers.ContentType!.MediaType);
  Assert.Equal(url,(await Json(await Send("GET",$"/api/employees/{id}"))).GetProperty("profilePhotoUrl").GetString());
  Assert.Equal(HttpStatusCode.NotFound,(await Send("GET",url,user:200,tenant:2)).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await Upload(bytes,102,version)).StatusCode);
 }
 [Fact] public async Task PersonalDetailsPersistAndSelfUpdatesCorrectly() {
  var created=await Json(await Send("POST","/api/employees",new{
   userId=103,employeeCode="PERS103",firstName="Personal",lastName="Test",joiningDate="2025-01-01",
   aadharNumber="123456789012",permanentAddress="123 Anna Salai, Chennai",
   emergencyContactName="John Doe",emergencyContactPhone="9876543210",emergencyContactRelation="Father"
  }));
  var id=created.GetProperty("id").GetInt64();
  var version=created.GetProperty("version").GetUInt32();
  Assert.Equal("123456789012",created.GetProperty("aadharNumber").GetString());
  Assert.Equal("123 Anna Salai, Chennai",created.GetProperty("permanentAddress").GetString());
  Assert.Equal("John Doe",created.GetProperty("emergencyContactName").GetString());
  Assert.Equal("9876543210",created.GetProperty("emergencyContactPhone").GetString());
  Assert.Equal("Father",created.GetProperty("emergencyContactRelation").GetString());

  var updated=await Json(await Send("PUT","/api/employees/me/profile",new{
   preferredName="Perry",phone="9998887776",
   aadharNumber="987654321098",permanentAddress="456 OMR Road, Chennai",
   emergencyContactName="Jane Doe",emergencyContactPhone="8887776665",emergencyContactRelation="Mother"
  },version:version,user:103,permissions:["EMPLOYEE_VIEW","EMPLOYEE_UPDATE_SELF"]));

  Assert.Equal("987654321098",updated.GetProperty("aadharNumber").GetString());
  Assert.Equal("456 OMR Road, Chennai",updated.GetProperty("permanentAddress").GetString());
  Assert.Equal("Jane Doe",updated.GetProperty("emergencyContactName").GetString());
  Assert.Equal("8887776665",updated.GetProperty("emergencyContactPhone").GetString());
  Assert.Equal("Mother",updated.GetProperty("emergencyContactRelation").GetString());
 }
 [Fact] public async Task PromotionPreservesHistoryAndRejectsInvalidDatesAndUnauthorizedChanges() {
  var t0=await Json(await Send("POST","/api/employees/references",new{kind="GRADE",name="T0"}));
  var t1=await Json(await Send("POST","/api/employees/references",new{kind="GRADE",name="T1"}));
  var employee=await Json(await Send("POST","/api/employees",new{userId=104,employeeCode="PROMO104",firstName="Career",lastName="Test",joiningDate="2025-01-01",gradeId=t0.GetProperty("id").GetInt64()}));
  var id=employee.GetProperty("id").GetInt64();var version=employee.GetProperty("version").GetUInt32();
  var eligible=await Json(await Send("GET","/api/employees/login-users"));
  Assert.DoesNotContain(eligible.GetProperty("items").EnumerateArray(),u=>u.GetProperty("id").GetInt64()==104);
  var duplicate=await Send("POST","/api/employees",new{userId=104,employeeCode="OTHER104",firstName="Career",lastName="Test",joiningDate="2025-01-01"});
  Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
  Assert.Equal("EMPLOYEE_ALREADY_EXISTS",(await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

  object Change(string? date)=>new{userId=104,employeeCode="PROMO104",firstName="Career",lastName="Test",joiningDate="2025-01-01",gradeId=t1.GetProperty("id").GetInt64(),careerEffectiveDate=date,careerChangeNote="Promotion to Junior"};
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("PUT",$"/api/employees/{id}",Change(null),version)).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("PUT",$"/api/employees/{id}",Change("2024-12-31"),version)).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("PUT",$"/api/employees/{id}",Change("2099-01-01"),version)).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("PUT",$"/api/employees/{id}",Change("2025-02-01"),version,user:104,permissions:["EMPLOYEE_VIEW","EMPLOYEE_UPDATE_SELF"])).StatusCode);
  var updated=await Json(await Send("PUT",$"/api/employees/{id}",Change("2025-02-01"),version));
  var history=await Json(await Send("GET",$"/api/employees/{id}/career-history",user:104,permissions:["EMPLOYEE_VIEW"]));
  Assert.Equal(2,history.GetArrayLength());Assert.Equal("T1",history[0].GetProperty("details").GetProperty("grade").GetString());
  Assert.Equal("Junior",history[0].GetProperty("details").GetProperty("designation").GetString());
  Assert.Equal("Intern",history[1].GetProperty("details").GetProperty("designation").GetString());
  await Json(await Send("PUT",$"/api/employees/{id}",Change(null),updated.GetProperty("version").GetUInt32()));
  Assert.Equal(2,(await Json(await Send("GET",$"/api/employees/{id}/career-history"))).GetArrayLength());
  Assert.Equal(HttpStatusCode.NotFound,(await Send("GET",$"/api/employees/{id}/career-history",user:200,tenant:2)).StatusCode);
  Assert.Equal(HttpStatusCode.OK,(await Send("GET",$"/api/employees/{id}/career-history",user:101,permissions:["EMPLOYEE_VIEW"])).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("GET",$"/api/employees/{id}/career-history",user:101,permissions:[])).StatusCode);
 }
 [Fact] public async Task EmployeeLifecycleSelfServiceHierarchySensitiveDataAndOutbox() {
  using(var seedScope=fixture.App.Services.CreateScope()) {
   var seedDb=seedScope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
   seedDb.CareerLevels.Add(new IdentityTenant.Domain.Careers.CareerLevel{CareerProfile=new(){Code="API_TEST",Name="Test career"},Grade=new(){Code="API_TEST",Name="Test grade"},CareerTitle="Test title"});
   await seedDb.SaveChangesAsync();
  }
  var catalog=await Json(await Send("GET","/api/employees/career-catalog"));
  var careerLevel=catalog.GetProperty("levels")[0];
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("GET","/api/employees/career-catalog",permissions:[])).StatusCode);
  var dept=await Json(await Send("POST","/api/employees/references",new{kind="DEPARTMENT",name="Engineering"}));
  var manager=await Json(await Send("POST","/api/employees",new{userId=100,employeeCode="EMP100",firstName="Manager",lastName="One",joiningDate="2025-01-01"}));
  var idManager=manager.GetProperty("id").GetInt64();
  var create=new{userId=101,employeeCode="EMP101",firstName="Developer",lastName="One",joiningDate="2025-01-01",departmentId=dept.GetProperty("id").GetInt64(),managerEmployeeId=idManager,careerProfileId=careerLevel.GetProperty("careerProfileId").GetInt64(),careerLevelId=careerLevel.GetProperty("id").GetInt64(),catalogGradeId=careerLevel.GetProperty("gradeId").GetInt64(),location=" Chennai ",designation="Junior"};
  var employee=await Json(await Send("POST","/api/employees",create));var id=employee.GetProperty("id").GetInt64();var v=employee.GetProperty("version").GetUInt32();
  Assert.True(employee.GetProperty("designationId").GetInt64()>0);
  Assert.Equal(create.careerLevelId,employee.GetProperty("careerLevelId").GetInt64());
  var locations=await Json(await Send("GET","/api/employees/references"));
  Assert.Contains(locations.EnumerateArray(),r=>r.GetProperty("kind").GetString()=="LOCATION"&&r.GetProperty("name").GetString()=="Chennai");
  Assert.Equal("user101@company.com",employee.GetProperty("workEmail").GetString());Assert.False(employee.TryGetProperty("ctc",out _));
  Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.Client.GetAsync("/api/employees")).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("GET","/api/employees",permissions:[])).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await Send("GET",$"/api/employees/{id}",user:200,tenant:2)).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST","/api/employees",new{userId=200,employeeCode="FOREIGN",firstName="No",lastName="Access",joiningDate="2025-01-01"})).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("PUT","/api/employees/me/profile",new{preferredName="Spoof",phone="",locationId=(long?)null,gradeId=1},v,user:101)).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("PUT",$"/api/employees/{id}",create,v,user:101,permissions:["EMPLOYEE_VIEW","EMPLOYEE_UPDATE_SELF"])).StatusCode);
  Assert.Equal((HttpStatusCode)428,(await Send("PUT","/api/employees/me/profile",new{preferredName="Dev",phone="123",locationId=(long?)null},user:101)).StatusCode);
  var self=await Json(await Send("PUT","/api/employees/me/profile",new{preferredName="Dev",phone="123",locationId=(long?)null},v,user:101));
  Assert.Equal("Dev",self.GetProperty("preferredName").GetString());
  Assert.Equal(HttpStatusCode.Conflict,(await Send("PUT","/api/employees/me/profile",new{preferredName="stale",phone="",locationId=(long?)null},v,user:101)).StatusCode);
  v=self.GetProperty("version").GetUInt32();
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("PUT","/api/employees/me/profile",new{careerProfileId=create.careerProfileId},v,user:101)).StatusCode);
  var moved=await Json(await Send("PUT","/api/employees/me/profile",new{location="Bengaluru"},v,user:101,permissions:["EMPLOYEE_VIEW","EMPLOYEE_UPDATE_SELF"]));
  v=moved.GetProperty("version").GetUInt32();Assert.NotEqual(employee.GetProperty("locationId").GetInt64(),moved.GetProperty("locationId").GetInt64());


  Assert.Equal(HttpStatusCode.BadRequest,(await Send("PUT","/api/employees/me/profile",new{profilePhotoUrl="javascript:alert(1)"},v,user:101)).StatusCode);
  var photo=await Json(await Send("PUT","/api/employees/me/profile",new{preferredName="Dev",phone="123",profilePhotoUrl="https://example.com/photo.jpg"},v,user:101));
  Assert.Equal("https://example.com/photo.jpg",photo.GetProperty("profilePhotoUrl").GetString());v=photo.GetProperty("version").GetUInt32();
  foreach(var section in new[]{"education","certifications","experience"}) {
   object body=section switch {
    "education"=>new{institution="University",qualification="BSc",fieldOfStudy="Computer Science",startDate="2020-01-01",endDate="2024-01-01"},
    "certifications"=>new{name="Cloud",issuer="Provider",issuedDate="2025-01-01",credentialId="CRED-123"},
    _=>new{company="Company",title="Engineer",description="Backend engineering",startDate="2024-01-01"}
   };
   var entry=await Json(await Send("POST",$"/api/employees/me/{section}",body,v,user:101,permissions:["EMPLOYEE_VIEW","EMPLOYEE_UPDATE_SELF"]));
   v=entry.GetProperty("version").GetUInt32();var entryId=entry.GetProperty("item").GetProperty("id").GetInt64();
   var entries=await Json(await Send("GET",$"/api/employees/me/{section}",user:101));
   Assert.Single(entries.EnumerateArray());
   if(section=="education")Assert.Equal("Computer Science",entries[0].GetProperty("fieldOfStudy").GetString());
   if(section=="certifications")Assert.Equal("CRED-123",entries[0].GetProperty("credentialId").GetString());
   var changed=await Json(await Send("PUT",$"/api/employees/me/{section}/{entryId}",body,v,user:101));v=changed.GetProperty("version").GetUInt32();
   var removed=await Send("DELETE",$"/api/employees/me/{section}/{entryId}",version:v,user:101);Assert.True(removed.IsSuccessStatusCode);
   var current=await Json(await Send("GET","/api/employees/me",user:101));v=current.GetProperty("version").GetUInt32();
   Assert.Empty((await Json(await Send("GET",$"/api/employees/me/{section}",user:101))).EnumerateArray());
  }
  var skill=await Json(await Send("POST","/api/employees/me/skills",new{skillName="CSharp",category="Backend",proficiency=4,yearsOfExperience=3,lastUsedDate="2025-01-01"},v,user:101,permissions:["EMPLOYEE_VIEW","EMPLOYEE_UPDATE_SELF"]));
  var skillId=skill.GetProperty("item").GetProperty("id").GetInt64();v=skill.GetProperty("version").GetUInt32();
  var masterId=skill.GetProperty("item").GetProperty("skillId").GetInt64();
  Assert.Equal(HttpStatusCode.Conflict,(await Send("POST","/api/employees/me/skills",new{skillId=masterId,proficiency=3,yearsOfExperience=1},v,user:101)).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST","/api/employees/me/skills",new{skillId=-1,proficiency=3,yearsOfExperience=1},v,user:101)).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("GET",$"/api/employees/{id}/skills",user:102,permissions:["EMPLOYEE_VIEW","EMPLOYEE_VIEW_TEAM"])).StatusCode);
  Assert.Equal(HttpStatusCode.OK,(await Send("GET",$"/api/employees/{id}/skills",user:100,permissions:["EMPLOYEE_VIEW","EMPLOYEE_VIEW_TEAM"])).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await Send("POST",$"/api/employees/{id}/skills",new{skillName="x",proficiency=3,yearsOfExperience=1},v,user:200,tenant:2)).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST",$"/api/employees/{id}/skills",new{skillName="x",proficiency=6,yearsOfExperience=-1},v)).StatusCode);
  var skillUpdate=await Json(await Send("PUT",$"/api/employees/{id}/skills/{skillId}",new{skillName="CSharp",category="Backend",proficiency=5,yearsOfExperience=4,lastUsedDate="2025-01-01"},v));v=skillUpdate.GetProperty("version").GetUInt32();
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("GET",$"/api/employees/{id}/compensation",user:101,permissions:["EMPLOYEE_VIEW"])).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("GET",$"/api/employees/{id}/compensation",user:101)).StatusCode);
  var comp=await Json(await Send("POST",$"/api/employees/{id}/compensation",new{ctc=100000,bonus=1000,currency="USD",effectiveFrom="2025-01-01"},v));v=comp.GetProperty("version").GetUInt32();
  Assert.Equal(HttpStatusCode.OK,(await Send("GET",$"/api/employees/{id}/compensation")).StatusCode);
  var list=await Json(await Send("GET",$"/api/employees?departmentId={create.departmentId}&search=Developer"));Assert.Equal(1,list.GetProperty("totalCount").GetInt32());
  var chart=await Json(await Send("GET",$"/api/employees/org-chart?managerId={idManager}"));Assert.Equal(id,chart.GetProperty("items")[0].GetProperty("id").GetInt64());
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("PUT",$"/api/employees/{idManager}",new{userId=100,employeeCode="EMP100",firstName="Manager",lastName="One",joiningDate="2025-01-01",managerEmployeeId=id},manager.GetProperty("version").GetUInt32())).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await Send("DELETE",$"/api/employees/{idManager}",version:manager.GetProperty("version").GetUInt32())).StatusCode);
  var updated=await Json(await Send("PUT",$"/api/employees/{id}",new{userId=101,employeeCode="EMP101",firstName="Updated",lastName="One",joiningDate="2025-01-01",careerEffectiveDate="2025-02-01"},v));v=updated.GetProperty("version").GetUInt32();
  Assert.Equal("Updated",updated.GetProperty("firstName").GetString());
  Assert.Equal(HttpStatusCode.NoContent,(await Send("DELETE",$"/api/employees/{id}",version:v)).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await Send("GET",$"/api/employees/{id}")).StatusCode);
  using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
  var payloads=await db.Database.SqlQueryRaw<string>("SELECT payload::text AS \"Value\" FROM employee_outbox").ToListAsync();
  foreach(var name in new[]{"EMPLOYEE_PROFILE_UPDATED","EMPLOYEE_EXPERIENCE_UPDATED","EMPLOYEE_EDUCATION_UPDATED","EMPLOYEE_CERTIFICATION_UPDATED"})Assert.Contains(payloads,x=>x.Contains(name));
  var transport=new RecordingTransport();
  using(var publisherServices=new ServiceCollection()
   .AddScoped<Persona.Infrastructure.Persistence.PersonaDbContext>(_=>new Persona.Infrastructure.Persistence.PersonaDbContext(new DbContextOptionsBuilder<Persona.Infrastructure.Persistence.PersonaDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options))
   .AddSingleton<Kafka.IKafkaProducer>(transport).BuildServiceProvider()) {
   using var publisher=new Persona.Api.Services.Employees.EmployeeOutboxPublisher(publisherServices.GetRequiredService<IServiceScopeFactory>(),Microsoft.Extensions.Logging.Abstractions.NullLogger<Persona.Api.Services.Employees.EmployeeOutboxPublisher>.Instance);
   await publisher.StartAsync(default);
   for(var retry=0;retry<40;retry++){
    var pending=await db.Database.SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM employee_outbox WHERE processed_at IS NULL").SingleAsync();
    if(pending==0)break;await Task.Delay(100);
   }
   await publisher.StopAsync(default);
   Assert.Equal(payloads.Count,transport.Events.Count);
   Assert.Equal(0,await db.Database.SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM employee_outbox WHERE processed_at IS NULL").SingleAsync());
  }
  var audit=scope.ServiceProvider.GetRequiredService<Audit.Application.Services.AuditLogService>();
  foreach(var payload in payloads){var message=Kafka.ReliableAuditConsumer.Parse(payload);
   Assert.True(await audit.RecordAsync(message.EventId,message.EventType,message.TenantId,message.PerformedByUserId,message.EntityType,message.EntityId,message.EntityKey,"IDENTITY_TENANT",message.OldValues,message.NewValues,message.Metadata,message.OccurredAt));
   Assert.False(await audit.RecordAsync(message.EventId,message.EventType,message.TenantId,message.PerformedByUserId,message.EntityType,message.EntityId,message.EntityKey,"IDENTITY_TENANT",message.OldValues,message.NewValues,message.Metadata,message.OccurredAt));
  }
  var auditResponse=await Json(await Send("GET","/api/audit/logs?EntityType=EMPLOYEE"));
  Assert.True(auditResponse.GetProperty("totalCount").GetInt32()>0);
  Assert.Contains(payloads,x=>x.Contains("EMPLOYEE_MANAGER_CHANGED"));Assert.Contains(payloads,x=>x.Contains("EMPLOYEE_DEPARTMENT_CHANGED"));Assert.Contains(payloads,x=>x.Contains("EMPLOYEE_CREATED"));Assert.Contains(payloads,x=>x.Contains("EMPLOYEE_SKILL_UPDATED"));Assert.Contains(payloads,x=>x.Contains("EMPLOYEE_DELETED"));Assert.DoesNotContain(payloads,x=>x.Contains("100000")||x.Contains("\"Ctc\""));
 }
 public sealed class RecordingTransport:Kafka.IKafkaProducer {
  private int calls;
  public System.Collections.Concurrent.ConcurrentBag<Messaging.Events.IssueAuditEvent> Events {get;}=[];
  public Task PublishAsync<T>(string topic,T message,CancellationToken ct=default){
   Assert.Equal("aiworkhub.issue.audit",topic);
   if(Interlocked.Increment(ref calls)==1)throw new InvalidOperationException("Simulated transient broker failure");
   Events.Add(Assert.IsType<Messaging.Events.IssueAuditEvent>(message));return Task.CompletedTask;
  }
 }
 [Fact] public async Task ProjectSummaryKeepsExistingJiraScope() {
  var admin=await Json(await Send("GET","/api/projects/by-user/104"));Assert.Contains(admin.EnumerateArray(),p=>p.GetProperty("id").GetInt64()==2);
  var peer=await Json(await Send("GET","/api/projects/by-user/104",user:101));Assert.Contains(peer.EnumerateArray(),p=>p.GetProperty("id").GetInt64()==2);
  var foreign=await Json(await Send("GET","/api/projects/by-user/104",user:200,tenant:2));Assert.Empty(foreign.EnumerateArray());
 }
 [Theory]
 [InlineData("/api/employees/me")]
 [InlineData("/api/employees/org-chart")]
 [InlineData("/api/employees/references")]
 public async Task AnonymousRejected(string path)=>Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.Client.GetAsync(path)).StatusCode);
}
