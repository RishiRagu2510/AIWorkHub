using System.Text.Json;
using System.Text.Json.Nodes;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIAgent.Api.Controllers;

[ApiController]
[Route("api/v1/agent/history")]
public class ChatHistoryController(IChatHistoryStore store, ICallerIdentityProvider identity, ILogger<ChatHistoryController> logger) : ControllerBase
{
    public sealed record ConsentRequest(string? Value);
    public sealed record SaveChatRequest(string? Title, long? UpdatedAt, List<JsonElement>? Messages);

    private async Task<(long TenantId, long UserId)?> CallerAsync(CancellationToken ct)
    {
        if (!identity.HasCredentials) return null;
        var caller = await identity.VerifyAsync(ct);
        return caller is { UserId: > 0, TenantId: > 0 } ? (caller.TenantId, caller.UserId) : null;
    }

    private async Task<IActionResult> GuardedAsync(Func<long, long, Task<IActionResult>> action, CancellationToken ct)
    {
        var caller = await CallerAsync(ct);
        if (caller is null) return Unauthorized(new { error = "Sign in again to use chat history." });
        try
        {
            return await action(caller.Value.TenantId, caller.Value.UserId);
        }
        catch (ChatHistoryUnavailableException ex)
        {
            logger.LogWarning(ex, "Chat history is unavailable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpGet("consent")]
    public Task<IActionResult> GetConsent(CancellationToken ct) =>
        GuardedAsync(async (tenant, user) => Ok(new { consent = await store.GetConsentAsync(tenant, user, ct) }), ct);

    [HttpPut("consent")]
    public Task<IActionResult> SetConsent([FromBody] ConsentRequest request, CancellationToken ct) =>
        GuardedAsync(async (tenant, user) =>
        {
            if (!ChatHistoryRules.IsValidConsent(request?.Value)) return BadRequest(new { error = "value must be yes or no." });
            await store.SetConsentAsync(tenant, user, request!.Value!, ct);
            return Ok(new { consent = request.Value });
        }, ct);

    [HttpGet("chats")]
    public Task<IActionResult> List(CancellationToken ct) =>
        GuardedAsync(async (tenant, user) => Ok(new { chats = await store.ListAsync(tenant, user, ct) }), ct);

    [HttpGet("chats/{chatId}")]
    public Task<IActionResult> Get(string chatId, CancellationToken ct) =>
        GuardedAsync(async (tenant, user) =>
        {
            if (!ChatHistoryRules.IsValidChatId(chatId)) return NotFound();
            var chat = await store.GetAsync(tenant, user, chatId, ct);
            if (chat is null) return NotFound();
            return Ok(new
            {
                id = chat.Id,
                title = chat.Title,
                updatedAt = chat.UpdatedAt,
                messages = new JsonArray(chat.Messages.Select(m => (JsonNode?)ChatHistoryRules.ToClientMessage(m)).ToArray())
            });
        }, ct);

    [HttpPut("chats/{chatId}")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public Task<IActionResult> Save(string chatId, [FromBody] SaveChatRequest request, CancellationToken ct) =>
        GuardedAsync(async (tenant, user) =>
        {
            var chat = ChatHistoryRules.TryBuild(chatId, request?.Title, request?.UpdatedAt, request?.Messages, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out var error);
            if (chat is null) return BadRequest(new { error });
            await store.SaveAsync(tenant, user, chat, ChatHistoryRules.MaxChats, ct);
            return Ok(new { id = chat.Id, title = chat.Title, updatedAt = chat.UpdatedAt, messageCount = chat.Messages.Count });
        }, ct);

    [HttpDelete("chats/{chatId}")]
    public Task<IActionResult> Delete(string chatId, CancellationToken ct) =>
        GuardedAsync(async (tenant, user) =>
        {
            if (!ChatHistoryRules.IsValidChatId(chatId)) return NotFound();
            return await store.DeleteAsync(tenant, user, chatId, ct) ? NoContent() : NotFound();
        }, ct);

    [HttpDelete("chats")]
    public Task<IActionResult> Clear(CancellationToken ct) =>
        GuardedAsync(async (tenant, user) => Ok(new { deleted = await store.ClearAsync(tenant, user, ct) }), ct);
}
