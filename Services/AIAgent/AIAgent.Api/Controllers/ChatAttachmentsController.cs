using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AIAgent.Api.Controllers;





[ApiController]
[Route("api/v1/agent/attachments")]
public class ChatAttachmentsController(IChatAttachmentStore store, ICallerIdentityProvider identity, IConfiguration config, ILogger<ChatAttachmentsController> logger) : ControllerBase
{
    private long MaxBytes => long.TryParse(config["Attachments:MaxBytes"], out var max) && max > 0 ? max : ChatAttachmentRules.DefaultMaxBytes;

    private async Task<(long TenantId, long UserId)?> CallerAsync(CancellationToken ct)
    {
        if (!identity.HasCredentials) return null;
        var caller = await identity.VerifyAsync(ct);
        return caller is { UserId: > 0, TenantId: > 0 } ? (caller.TenantId, caller.UserId) : null;
    }

    private async Task<IActionResult> GuardedAsync(Func<long, long, Task<IActionResult>> action, CancellationToken ct)
    {
        var caller = await CallerAsync(ct);
        if (caller is null) return Unauthorized(new { error = "Sign in again to attach files." });
        try
        {
            return await action(caller.Value.TenantId, caller.Value.UserId);
        }
        catch (ChatAttachmentUnavailableException ex)
        {
            logger.LogWarning(ex, "Chat attachments are unavailable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpPost]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> Upload(IFormFile? file, CancellationToken ct) =>
        GuardedAsync(async (tenant, user) =>
        {
            if (file is null) return BadRequest(new { error = "Choose a file to attach." });

            var name = ChatAttachmentRules.SafeName(file.FileName);
            await using var stream = file.OpenReadStream();
            var head = new byte[ChatAttachmentRules.HeadBytes];
            var read = 0;
            while (read < head.Length)
            {
                var n = await stream.ReadAsync(head.AsMemory(read), ct);
                if (n == 0) break;
                read += n;
            }

            var problem = ChatAttachmentRules.Validate(name, file.Length, head.AsSpan(0, read), MaxBytes);
            if (problem is not null) return BadRequest(new { error = problem });

            stream.Position = 0;
            ChatAttachmentRules.TryContentType(name, out var contentType);
            var saved = await store.SaveAsync(tenant, user, name, contentType, stream, file.Length, ct);
            return StatusCode(StatusCodes.Status201Created, new { id = saved.Id, name = saved.Name, contentType = saved.ContentType, size = saved.Size });
        }, ct);

    [HttpGet("{id}")]
    public Task<IActionResult> Download(string id, CancellationToken ct) =>
        GuardedAsync(async (tenant, user) =>
        {
            var found = await store.OpenAsync(tenant, user, id, ct);
            if (found is null) return NotFound(new { error = "That file is no longer available." });

            var (info, content) = found.Value;
            Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            Response.Headers[HeaderNames.ContentSecurityPolicy] = "sandbox; default-src 'none'";
            Response.Headers[HeaderNames.CacheControl] = "private, max-age=3600";
            
            var disposition = new ContentDispositionHeaderValue(ChatAttachmentRules.IsImage(info.ContentType) ? "inline" : "attachment");
            disposition.SetHttpFileName(info.Name);
            Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
            return File(content, info.ContentType);
        }, ct);

    [HttpDelete("{id}")]
    public Task<IActionResult> Remove(string id, CancellationToken ct) =>
        GuardedAsync(async (tenant, user) => await store.DeleteAsync(tenant, user, id, ct) ? NoContent() : NotFound(), ct);
}
