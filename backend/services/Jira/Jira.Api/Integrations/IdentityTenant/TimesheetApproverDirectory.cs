using System.Net.Http.Headers;
using Jira.Application.Abstractions.Services;
using Jira.Application.Services;
using Jira.Contracts.Timesheets;
namespace Jira.Api.Integrations.IdentityTenant;
public sealed class TimesheetApproverDirectory(HttpClient client,IHttpContextAccessor accessor):ITimesheetApproverDirectory
{
    public async Task<IReadOnlyList<TimesheetApprover>> ListAsync(CancellationToken ct) {
        try {
            if(!AuthenticationHeaderValue.TryParse(accessor.HttpContext?.Request.Headers.Authorization,out var bearer)||bearer.Scheme!="Bearer")throw new HttpRequestException();
            var results=new List<TimesheetApprover>();
            for(var page=1;page<=100;page++) {
                using var request=new HttpRequestMessage(HttpMethod.Get,$"api/users/timesheet-approvers?page={page}");request.Headers.Authorization=bearer;
                using var response=await client.SendAsync(request,ct);response.EnsureSuccessStatusCode();
                var items=await response.Content.ReadFromJsonAsync<TimesheetApprover[]>(ct)??throw new HttpRequestException();
                results.AddRange(items);if(items.Length<100)return results;
            }
            throw new HttpRequestException("Approver directory limit exceeded.");
        }catch(Exception e)when(e is HttpRequestException or System.Text.Json.JsonException || e is TaskCanceledException&&!ct.IsCancellationRequested){throw new UserDirectoryUnavailableException(e);}
    }
}
