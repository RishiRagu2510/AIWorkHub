using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Persona.Api.Services.Employees;
namespace Backend.Authorization.Tests;
public sealed class EmployeeAbsenceTests(SecurityFixture fixture):IClassFixture<SecurityFixture>
{
 async Task<HttpResponseMessage> Send(string method,string path,object? body=null,long user=100,long tenant=1,string[]? permissions=null) {
  using var r=new HttpRequestMessage(new HttpMethod(method),path);
  r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,tenant,user==100?"ADMIN":"DEVELOPER",permissions??SecurityFixture.Permissions));
  if(body!=null)r.Content=JsonContent.Create(body);
  return await fixture.Client.SendAsync(r);
 }
 static async Task<JsonElement> Json(HttpResponseMessage response) {Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());return await response.Content.ReadFromJsonAsync<JsonElement>();}
 [Fact] public void CountsWorkingDaysAndSkipsHolidays() {
  Assert.Equal(4,AbsenceService.WorkingDays(new(2027,1,4),new(2027,1,10),new HashSet<DateOnly>{new(2027,1,6)}));
  Assert.Equal(0,AbsenceService.WorkingDays(new(2027,1,9),new(2027,1,10),new HashSet<DateOnly>()));
 }
 [Fact] public async Task BalancesApprovalsIsolationAndTransitionsAreEnforced() {
  const string path="/api/employees/absence";
  Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.Client.GetAsync(path+"?year=2027")).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("GET",path+"/policy?year=2027",permissions:[])).StatusCode);
  var manager=await Json(await Send("POST","/api/employees",new{userId=100,employeeCode="M100",firstName="Manager",lastName="Test",joiningDate="2025-01-01"}));
  var employee=await Json(await Send("POST","/api/employees",new{userId=101,employeeCode="D101",firstName="Employee",lastName="Test",joiningDate="2025-01-01",managerEmployeeId=manager.GetProperty("id").GetInt64()}));
  await Json(await Send("POST","/api/employees",new{userId=102,employeeCode="D102",firstName="Other",lastName="Test",joiningDate="2025-01-01"}));
  var request=new{kind="CASUAL",startDate="2027-01-04",endDate="2027-01-05",reason="Family event"};
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST",path,request,user:101)).StatusCode);
  var policy=new{casualDays=2,sickDays=12,earnedDays=12,holidays=Array.Empty<string>(),version=0};
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("PUT",path+"/policy?year=2027",policy,user:101,permissions:["EMPLOYEE_VIEW"])).StatusCode);
  await Json(await Send("PUT",path+"/policy?year=2027",policy));
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST",path,request,user:102)).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST",path,new{kind="CASUAL",startDate="2027-01-04",endDate="2027-01-06",reason="Too many"},user:101)).StatusCode);
  var created=await Json(await Send("POST",path,request,user:101));var id=created.GetProperty("id").GetInt64();
  var summary=await Json(await Send("GET",path+"?year=2027",user:101));
  Assert.Equal(0,summary.GetProperty("balances")[0].GetProperty("available").GetInt32());
  Assert.Equal(2,summary.GetProperty("balances")[0].GetProperty("pending").GetInt32());
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST",path,new{kind="WFH",startDate="2027-01-05",endDate="2027-01-05",reason="Overlap"},user:101)).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await Send("PUT",path+"/policy?year=2027",new{casualDays=3,sickDays=12,earnedDays=12,holidays=Array.Empty<string>(),version=1})).StatusCode);
  var decision=new{action="APPROVE",version=1};
  Assert.Equal(HttpStatusCode.NotFound,(await Send("POST",path+$"/{id}/decision",decision,user:200,tenant:2)).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("POST",path+$"/{id}/decision",decision,user:101)).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Send("POST",path+$"/{id}/decision",decision,user:102)).StatusCode);
  Assert.Empty((await Json(await Send("GET",path+"?year=2027&team=true",user:102))).GetProperty("items").EnumerateArray());
  Assert.Single((await Json(await Send("GET",path+"?year=2027&team=true"))).GetProperty("items").EnumerateArray());
  await Json(await Send("POST",path+$"/{id}/decision",decision,permissions:["EMPLOYEE_VIEW"]));
  Assert.Equal(HttpStatusCode.Conflict,(await Send("POST",path+$"/{id}/decision",decision)).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await Send("POST",path+$"/{id}/decision",new{action="CANCEL",version=1},user:101)).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await Send("POST",path+$"/{id}/decision",new{action="CANCEL",version=2},user:101)).StatusCode);
  summary=await Json(await Send("GET",path+"?year=2027",user:101));
  Assert.Equal(0,summary.GetProperty("balances")[0].GetProperty("available").GetInt32());
  created=await Json(await Send("POST",path,new{kind="WFH",startDate="2027-01-06",endDate="2027-01-07",reason="WFH"},user:101));id=created.GetProperty("id").GetInt64();
  Assert.Equal("PENDING",created.GetProperty("status").GetString());
  Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST",path+$"/{id}/decision",new{action="REJECT",version=1})).StatusCode);
  await Json(await Send("POST",path+$"/{id}/decision",new{action="REJECT",version=1,note="Office attendance needed"}));
  summary=await Json(await Send("GET",path+"?year=2027",user:101));
  Assert.Equal(0,summary.GetProperty("balances")[0].GetProperty("available").GetInt32());
 }
}
