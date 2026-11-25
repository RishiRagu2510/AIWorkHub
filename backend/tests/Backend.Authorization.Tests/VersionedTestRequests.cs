using System.Net.Http.Headers;

namespace Backend.Authorization.Tests;

internal static class VersionedTestRequests
{
    
    
    public static async Task AddCurrentVersionAsync(HttpClient client, HttpRequestMessage request)
    {
        if (request.Method != HttpMethod.Put && request.Method != HttpMethod.Delete) return;
        var path = request.RequestUri!.OriginalString.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (path.Length < 3 || path[0] != "api" || path[1] is not ("issues" or "projects") || !long.TryParse(path[2], out _)) return;
        if (path.Contains("members") || path.Contains("comments")) return;
        using var read = new HttpRequestMessage(HttpMethod.Get, $"/api/{path[1]}/{path[2]}");
        read.Headers.Authorization = request.Headers.Authorization;
        using var response = await client.SendAsync(read);
        request.Headers.IfMatch.Add(response.Headers.ETag ?? new EntityTagHeaderValue("\"1\""));
    }
}
