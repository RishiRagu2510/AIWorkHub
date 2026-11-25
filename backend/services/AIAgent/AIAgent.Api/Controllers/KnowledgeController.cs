using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Infrastructure.Knowledge;
using Microsoft.AspNetCore.Mvc;

namespace AIAgent.Api.Controllers;

[ApiController]
[Route("api/v1/agent/knowledge")]
public class KnowledgeController(IKnowledgeStore store, ICallerIdentityProvider identity, ILogger<KnowledgeController> logger) : ControllerBase
{
    private async Task<IActionResult> GuardedAsync(Func<VerifiedCaller, Task<IActionResult>> action, CancellationToken ct)
    {
        var caller = identity.HasCredentials ? await identity.VerifyAsync(ct) : null;
        if (caller is not { UserId: > 0, TenantId: > 0 }) return Unauthorized(new { error = "Sign in again to manage company documents." });
        if (!KnowledgeRules.CanManage(caller.Roles.Append(caller.Role)))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Only Admin and HR can manage company documents." });
        try
        {
            return await action(caller);
        }
        catch (KnowledgeUnavailableException ex)
        {
            logger.LogWarning(ex, "The company documents are unavailable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    private static object Shape(KnowledgeDocument d) => new
    {
        id = d.Id,
        title = d.Title,
        fileName = d.FileName,
        roles = d.AllowedRoles,
        everyone = d.AllowedRoles.Count == 0,
        version = d.Version,
        passages = d.Passages,
        characters = d.Characters,
        updatedAt = d.UpdatedAt
    };

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) =>
        GuardedAsync(async caller =>
        {
            var documents = await store.ListAsync(caller.TenantId, ct);
            return Ok(new { documents = documents.Select(Shape), limit = KnowledgeRules.MaxDocuments });
        }, ct);

    [HttpGet("{id:long}")]
    public Task<IActionResult> Get(long id, CancellationToken ct) =>
        GuardedAsync(async caller =>
        {
            var doc = await store.GetAsync(caller.TenantId, id, ct);
            if (doc is null) return NotFound(new { error = "Document not found." });
            return Ok(new
            {
                id = doc.Id,
                title = doc.Title,
                fileName = doc.FileName,
                roles = doc.AllowedRoles,
                everyone = doc.AllowedRoles.Count == 0,
                version = doc.Version,
                passages = doc.Passages,
                characters = doc.Characters,
                updatedAt = doc.UpdatedAt,
                content = doc.Content
            });
        }, ct);

    [HttpPost]
    [RequestSizeLimit(KnowledgeRules.MaxFileBytes + 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> Upload(IFormFile? file, [FromForm] string? title, [FromForm] string[]? roles, CancellationToken ct) =>
        GuardedAsync(async caller =>
        {
            if (file is null) return BadRequest(new { error = "Choose a file to upload." });

            var name = ChatAttachmentRules.SafeName(file.FileName);
            if (!KnowledgeRules.ExtensionAllowed(name))
                return BadRequest(new { error = $"That file type can't be used here. Upload one of: {string.Join(", ", KnowledgeRules.Extensions)}." });

            var clean = (title ?? "").Trim();
            if (clean.Length == 0) clean = Path.GetFileNameWithoutExtension(name).Replace('_', ' ').Replace('-', ' ').Trim();
            if (KnowledgeRules.TitleProblem(clean) is { } titleProblem) return BadRequest(new { error = titleProblem });

            var (audience, roleProblem) = KnowledgeRules.NormalizeRoles(roles);
            if (roleProblem is not null) return BadRequest(new { error = roleProblem });

            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, ct);
            var bytes = memory.ToArray();

            var problem = ChatAttachmentRules.Validate(name, bytes.Length, bytes.AsSpan(0, Math.Min(bytes.Length, ChatAttachmentRules.HeadBytes)), KnowledgeRules.MaxFileBytes);
            if (problem is not null) return BadRequest(new { error = problem });

            var text = KnowledgeTextExtractor.Extract(bytes, name);
            if (text is null)
                return BadRequest(new { error = "I couldn't read any text in that file. A scanned page or a picture has no text to search, and a locked or damaged file can't be opened." });

            var chunks = KnowledgeChunker.Split(text);
            if (chunks.Count == 0) return BadRequest(new { error = "That file has no text in it." });

            var outcome = await store.SaveAsync(caller.TenantId, caller.UserId,
                new NewKnowledgeDocument(clean, name, KnowledgeRules.TypeName(name), audience, text, chunks), ct);

            if (outcome.Status == KnowledgeSaveStatus.Full)
                return Conflict(new { error = $"The company already has {KnowledgeRules.MaxDocuments} documents. Remove one before adding another." });

            var status = outcome.Status.ToString().ToLowerInvariant();
            var body = new { status, document = outcome.Document is null ? null : Shape(outcome.Document) };
            return outcome.Status == KnowledgeSaveStatus.Added ? StatusCode(StatusCodes.Status201Created, body) : Ok(body);
        }, ct);

    [HttpDelete("{id:long}")]
    public Task<IActionResult> Delete(long id, CancellationToken ct) =>
        GuardedAsync(async caller => await store.DeleteAsync(caller.TenantId, id, ct) ? NoContent() : NotFound(), ct);
}
