using System.Text.Json;

namespace AIAgent.Infrastructure.Tools;


internal sealed record DirectoryEntry(
    long EmployeeId,
    long UserId,
    string Code,
    string Name,
    string? Email,
    long? ManagerEmployeeId,
    long? DesignationId,
    long? GradeId,
    long? DepartmentId,
    long? JobTitleId,
    string? Status);

internal static class EmployeeDirectory
{
    private const int PageSize = 100;
    private const int MaxPages = 5;

    public static async Task<IReadOnlyList<DirectoryEntry>> LoadAsync(HttpClient identity, CancellationToken ct, string? search = null)
    {
        var people = new List<DirectoryEntry>();
        try
        {
            for (var page = 1; page <= MaxPages; page++)
            {
                var url = $"{IdentityRoutes.Employees}?Page={page}&PageSize={PageSize}";
                if (!string.IsNullOrWhiteSpace(search)) url += $"&Search={Uri.EscapeDataString(search)}";

                using var response = await identity.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) break;

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) break;

                foreach (var item in items.EnumerateArray())
                {
                    var employeeId = Long(item, "id");
                    if (employeeId == null) continue;
                    var first = Str(item, "firstName") ?? "";
                    var last = Str(item, "lastName") ?? "";
                    people.Add(new DirectoryEntry(
                        employeeId.Value,
                        Long(item, "userId") ?? 0,
                        Str(item, "employeeCode") ?? "",
                        $"{first} {last}".Trim(),
                        Str(item, "workEmail"),
                        Long(item, "managerEmployeeId"),
                        Long(item, "designationId"),
                        Long(item, "gradeId"),
                        Long(item, "departmentId"),
                        Long(item, "jobTitleId"),
                        Str(item, "employmentStatus")));
                }

                var totalPages = root.TryGetProperty("totalPages", out var tp) && tp.ValueKind == JsonValueKind.Number ? tp.GetInt32() : 1;
                if (page >= totalPages) break;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            
        }

        return people;
    }

    
    public static async Task<IReadOnlyDictionary<long, (string Kind, string Name)>> ReferencesAsync(HttpClient identity, CancellationToken ct)
    {
        var map = new Dictionary<long, (string, string)>();
        try
        {
            using var response = await identity.GetAsync(IdentityRoutes.EmployeeReferences, ct);
            if (!response.IsSuccessStatusCode) return map;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return map;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var id = Long(item, "id");
                var name = Str(item, "name");
                if (id != null && name != null) map[id.Value] = (Str(item, "kind") ?? "", name);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
        }

        return map;
    }

    
    public static string? NameOfUser(IEnumerable<DirectoryEntry> people, long? userId) =>
        userId is null ? null : people.FirstOrDefault(p => p.UserId == userId)?.Name;

    private static long? Long(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
