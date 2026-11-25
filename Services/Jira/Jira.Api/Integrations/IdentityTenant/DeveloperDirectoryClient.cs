using System.Net;
using System.Net.Http.Headers;
using Jira.Application.Abstractions.Services;

namespace Jira.Api.Integrations.IdentityTenant;

public sealed class DeveloperDirectoryClient(HttpClient client, IHttpContextAccessor accessor) : IDeveloperDirectory
{
    public async Task<bool> IsActiveDeveloperAsync(long tenantId, long userId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/users/{userId}");
        if (!AuthenticationHeaderValue.TryParse(accessor.HttpContext?.Request.Headers.Authorization, out var bearer) || bearer.Scheme != "Bearer")
            throw new HttpRequestException("An authenticated bearer context is required.");
        request.Headers.Authorization = bearer;
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<DirectoryUser>(ct);
        return user is not null && user.Id == userId && user.TenantId == tenantId;
    }
    public async Task<AssignmentUser?> GetAssignmentUserAsync(long tenantId, long userId, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/users/{userId}/assignment-eligibility");
            if (!AuthenticationHeaderValue.TryParse(accessor.HttpContext?.Request.Headers.Authorization, out var bearer) ||
                !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
                throw new HttpRequestException("An authenticated bearer context is required.");
            request.Headers.Authorization = bearer;
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var user = await response.Content.ReadFromJsonAsync<AssignmentUser>(ct);
            return user is not null && user.Id == userId && user.TenantId == tenantId ? user : null;
        }
        catch (System.Text.Json.JsonException ex) { throw new Jira.Application.Services.UserDirectoryUnavailableException(ex); }
        catch (HttpRequestException ex) { throw new Jira.Application.Services.UserDirectoryUnavailableException(ex); }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        { throw new Jira.Application.Services.UserDirectoryUnavailableException(ex); }
    }
    public async Task<CollaborationUser?> GetCollaborationUserAsync(long tenantId,long userId,CancellationToken ct)
    {
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,$"api/users/{userId}/collaboration-eligibility");
            if(!AuthenticationHeaderValue.TryParse(accessor.HttpContext?.Request.Headers.Authorization,out var bearer))throw new HttpRequestException();
            request.Headers.Authorization=bearer;using var response=await client.SendAsync(request,ct);
            if(response.StatusCode==HttpStatusCode.NotFound)return null;response.EnsureSuccessStatusCode();
            var user=await response.Content.ReadFromJsonAsync<CollaborationUser>(ct);
            return user?.Id==userId&&user.TenantId==tenantId&&user.Roles is not null?user:null;
        }
        catch(Exception ex)when(ex is HttpRequestException or System.Text.Json.JsonException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {throw new Jira.Application.Services.UserDirectoryUnavailableException(ex);}
    }
    private sealed record DirectoryUser(long Id, long TenantId);
}
