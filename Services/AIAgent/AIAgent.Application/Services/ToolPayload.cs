using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIAgent.Application.Services;


public static class ToolPayload
{
    public static object? Unpack(object? data)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            switch (data)
            {
                case null:
                    return null;
                case string text:
                {
                    var trimmed = text.Trim();
                    if (trimmed.Length == 0) return text;
                    if (trimmed[0] is not ('{' or '[' or '"')) return text;
                    try
                    {
                        data = JsonSerializer.Deserialize<JsonElement>(trimmed);
                    }
                    catch (JsonException)
                    {
                        return text;
                    }
                    continue;
                }
                case JsonElement { ValueKind: JsonValueKind.String } element:
                    data = element.GetString();
                    continue;
                default:
                    return data;
            }
        }

        return data;
    }
    
    public static object? ForModel(object? unpacked)
    {
        if (unpacked is null) return null;

        JsonNode? node;
        try
        {
            node = unpacked is JsonElement element ? JsonNode.Parse(element.GetRawText()) : JsonSerializer.SerializeToNode(unpacked);
        }
        catch (JsonException)
        {
            return unpacked;
        }

        if (node is JsonObject obj && FindFile(obj) is { } file && file["content"] is JsonValue content)
        {
            var bytes = System.Text.Encoding.UTF8.GetByteCount(content.ToString());
            file.Remove("content");
            file["contentWithheldFromAssistant"] = true;
            file["bytes"] = bytes;
        }

        return node;
    }

    private static JsonObject? FindFile(JsonObject obj) =>
        (obj.TryGetPropertyValue("file", out var f) ? f : null) as JsonObject;
}
