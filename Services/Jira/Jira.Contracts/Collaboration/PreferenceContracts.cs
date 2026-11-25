using System.Text.Json;

namespace Jira.Contracts.Collaboration;


public sealed record UpdatePreferenceRequest(JsonElement Value, uint Version);

public sealed record PreferenceDto(string Key, JsonElement Value, uint Version);
