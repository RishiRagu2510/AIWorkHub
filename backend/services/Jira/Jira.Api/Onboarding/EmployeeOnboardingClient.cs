using System.Net.Http.Headers;
using System.Text.Json;
namespace Jira.Api.Onboarding;
public sealed class EmployeeOnboardingClient(HttpClient client,IHttpContextAccessor accessor)
{
 public async Task<(int Status,JsonElement Body)> Send(string path, object body,CancellationToken ct,KeyValuePair<string,string>? header=null) {
  if(!AuthenticationHeaderValue.TryParse(accessor.HttpContext?.Request.Headers.Authorization,out var bearer)||bearer.Scheme!="Bearer")return(401,JsonSerializer.SerializeToElement(new{message="Authentication is required."}));
  try {
   using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};request.Headers.Authorization=bearer;if(header is {} extra)request.Headers.Add(extra.Key,extra.Value);
   using var response=await client.SendAsync(request,ct);
   var text=await response.Content.ReadAsStringAsync(ct);
   return((int)response.StatusCode,string.IsNullOrWhiteSpace(text)?JsonSerializer.SerializeToElement(new{message="Employee service refused the request."}):JsonSerializer.Deserialize<JsonElement>(text));
  }catch(Exception ex)when(ex is HttpRequestException or JsonException || ex is TaskCanceledException&&!ct.IsCancellationRequested){return(503,JsonSerializer.SerializeToElement(new{message="Employee service is unavailable. Retry shortly."}));}
 }
}
