using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;
public sealed class AllocationTests(SecurityFixture fixture):IClassFixture<SecurityFixture> {
 async Task<HttpResponseMessage> Send(HttpMethod method,string path,object? body=null,long user=100,long tenant=1,string role="ADMIN") {
 var r=new HttpRequestMessage(method,path);r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,tenant,role,["PROJECT_MANAGE","PROJECT_VIEW","ISSUE_ASSIGN"]));if(body!=null)r.Content=JsonContent.Create(body);return await fixture.Client.SendAsync(r);
 }
 [Fact] public async Task AllocationsEnforceScopeCapacityAndDates() {
 using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
 await db.Database.ExecuteSqlRawAsync(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"025_allocations.sql")));
 object Body(long project,int percent,string start="2026-01-01",string? end=null)=>new {projectId=project,userId=100,percentage=percent,startDate=start,endDate=end};
 Assert.Equal(HttpStatusCode.Created,(await Send(HttpMethod.Post,"/api/projects/allocations",Body(1,60))).StatusCode);
 Assert.Equal(HttpStatusCode.Conflict,(await Send(HttpMethod.Post,"/api/projects/allocations",Body(2,50))).StatusCode);
 Assert.Equal(HttpStatusCode.Created,(await Send(HttpMethod.Post,"/api/projects/allocations",Body(2,40))).StatusCode);
 Assert.Equal(HttpStatusCode.Conflict,(await Send(HttpMethod.Post,"/api/projects/allocations",Body(1,1))).StatusCode);
 Assert.Equal(HttpStatusCode.BadRequest,(await Send(HttpMethod.Post,"/api/projects/allocations",Body(1,10,"2026-02-01","2026-01-01"))).StatusCode);
 Assert.Equal(HttpStatusCode.NotFound,(await Send(HttpMethod.Post,"/api/projects/allocations",Body(3,10))).StatusCode);
 Assert.Equal(HttpStatusCode.Forbidden,(await Send(HttpMethod.Get,"/api/projects/allocations?userId=100",user:101,role:"DEVELOPER")).StatusCode);
 Assert.Equal(HttpStatusCode.Forbidden,(await Send(HttpMethod.Post,"/api/projects/allocations",Body(1,10),user:101,role:"DEVELOPER")).StatusCode);
 var rows=await (await Send(HttpMethod.Get,"/api/projects/allocations")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();Assert.Equal(2,rows.GetArrayLength());
 var id=rows[0].GetProperty("id").GetInt64();
 Assert.Equal(HttpStatusCode.NotFound,(await Send(HttpMethod.Delete,$"/api/projects/allocations/{id}",user:200,tenant:2)).StatusCode);
 Assert.Equal(HttpStatusCode.NoContent,(await Send(HttpMethod.Delete,$"/api/projects/allocations/{id}")).StatusCode);
 }
}
