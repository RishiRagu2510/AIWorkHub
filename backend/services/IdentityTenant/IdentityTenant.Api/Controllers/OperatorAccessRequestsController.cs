using System.Net;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Application.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("operator/access-requests")]
public sealed class OperatorAccessRequestsController(
    TenantPlanService plans,
    OperatorActionTokens tokens,
    ILogger<OperatorAccessRequestsController> logger) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Review(string token, CancellationToken ct)
    {
        var claim = tokens.Verify(token, DateTimeOffset.UtcNow);
        if (claim is null)
            return Page(StatusCodes.Status400BadRequest, "This link is not valid",
                "It may have expired (links last 7 days), been replaced when the operator key changed, or been copied incorrectly. You can still decide the request with the operator API, or ask the company to request it again.");

        var found = await plans.FindRequestForReviewAsync(claim.TenantId, claim.RequestId, ct);
        if (found is null)
            return Page(StatusCodes.Status200OK, "Already handled",
                "This request was already approved, declined or cancelled. There is nothing left to do.");

        var (company, request) = found;
        var what = TenantAccessRequests.DisplayName(request.Kind, request.Name);
        var approve = claim.Action == OperatorAction.Approve;
        var cancel = request.IsCancellation;
        var verb = approve ? cancel ? "Approve cancellation of" : "Approve" : cancel ? "Keep" : "Decline";
        var details = $"""
                       <p><strong>{H(company)}</strong> (tenant {claim.TenantId}) asked {(cancel ? "to cancel" : "for")} <strong>{H(what)}</strong>.</p>
                       <dl>
                         <dt>Asked by</dt><dd>{H(request.UserName)} (user {request.UserId})</dd>
                         <dt>When</dt><dd>{request.CreatedAt:dd MMM yyyy HH:mm} UTC</dd>
                         <dt>Note</dt><dd>{(string.IsNullOrWhiteSpace(request.Note) ? "(none)" : H(request.Note))}</dd>
                       </dl>
                       <p class="hint">{Hint(approve, cancel, what, request.Name)}</p>
                       <form method="post">
                         <button type="submit" class="{(approve ? cancel ? "decline" : "approve" : cancel ? "approve" : "decline")}">{verb} {H(what)} for {H(company)}</button>
                       </form>
                       """;
        return Page(StatusCodes.Status200OK,
            approve ? cancel ? $"Cancel {what}?" : $"Approve {what}?" : cancel ? $"Keep {what}?" : $"Decline {what}?",
            details, true);
    }

    [HttpPost("{token}")]
    public async Task<IActionResult> Decide(string token, CancellationToken ct)
    {
        var claim = tokens.Verify(token, DateTimeOffset.UtcNow);
        if (claim is null)
            return Page(StatusCodes.Status400BadRequest, "This link is not valid",
                "It may have expired or been copied incorrectly. Nothing was changed.");

        var found = await plans.FindRequestForReviewAsync(claim.TenantId, claim.RequestId, ct);
        var result = claim.Action == OperatorAction.Approve
            ? await plans.ApproveAsync(claim.TenantId, claim.RequestId, ct)
            : await plans.DeclineAsync(claim.TenantId, claim.RequestId, ct);

        switch (result.Outcome)
        {
            case DecisionOutcome.NotFound:
                return Page(StatusCodes.Status200OK, "Already handled",
                    "This request was already approved, declined or cancelled. Nothing was changed.");
            case DecisionOutcome.LastPortal:
                return Page(StatusCodes.Status409Conflict, "Cannot cancel the last portal",
                    $"{TenantAccessRequests.DisplayName(result.Request!.Kind, result.Request.Name)} is the only portal left in this company's plan, and a plan has to include at least one. Nothing was changed. Decline the request, or end the plan another way.");
            case DecisionOutcome.NeedsPortal:
                return Page(StatusCodes.Status409Conflict, "Approve AIWorkHub first",
                    $"{TenantAccessRequests.DisplayName(result.Request!.Kind, result.Request.Name)} comes with {PortalName(result.Needs)}, which this company does not have yet. Nothing was changed.");
        }

        var what = TenantAccessRequests.DisplayName(result.Request!.Kind, result.Request.Name);
        var company = found?.Company ?? $"Tenant {claim.TenantId}";
        logger.LogInformation("Operator {Action} tenant {TenantId}'s request for {Kind} {Name} from the email link.",
            claim.Action == OperatorAction.Approve ? "approved" : "declined", claim.TenantId, result.Request.Kind,
            result.Request.Name);

        var cancelled = result.Request.IsCancellation;
        var chatToo = cancelled && result.Request is { Kind: TenantAccessRequests.PortalKind, Name: "hub" }
            ? " The Athenna AI chat goes with it."
            : "";
        if (claim.Action == OperatorAction.Approve)
            return cancelled
                ? Page(StatusCodes.Status200OK, "Cancelled",
                    $"{what} is no longer part of {company}'s plan.{chatToo} Their people lose it the next time they sign in.")
                : Page(StatusCodes.Status200OK, "Approved",
                    $"{what} is now part of {company}'s plan. Their people see it the next time they sign in.");
        return cancelled
            ? Page(StatusCodes.Status200OK, "Kept", $"{company} keeps {what}. The plan did not change.")
            : Page(StatusCodes.Status200OK, "Declined",
                $"The request for {what} from {company} was declined. The plan did not change.");
    }

    private static string Hint(bool approve, bool cancel, string what, string name)
    {
        if (approve && cancel)
            return
                $"Approving removes {H(what)} from this company's plan straight away{(name == "hub" ? ", and the Athenna AI chat with it" : "")}. Their people lose it the next time they sign in.";
        if (approve)
            return
                $"Approving adds {H(what)} to this company's plan straight away. Their people see it the next time they sign in.";
        return cancel
            ? "The plan does not change and the company keeps what it has."
            : "Declining removes the request. The plan does not change, and the company can ask again.";
    }

    private static string PortalName(string? portal)
    {
        return portal is null
            ? "another portal"
            : TenantAccessRequests.DisplayName(TenantAccessRequests.PortalKind, portal);
    }

    private static string H(string? text)
    {
        return WebUtility.HtmlEncode(text ?? "");
    }


    private ContentResult Page(int status, string title, string body, bool raw = false)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["X-Frame-Options"] = "DENY";
        Response.Headers["Content-Security-Policy"] =
            "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'";
        var content = raw ? body : $"<p>{H(body)}</p>";
        var html = $$"""
                     <!doctype html>
                     <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="noindex">
                     <title>{{H(title)}} - AIWorkHub</title>
                     <style>
                       body{margin:0;background:#f8fafc;color:#0f172a;font:16px/1.5 system-ui,-apple-system,Segoe UI,Roboto,sans-serif}
                       main{max-width:520px;margin:12vh auto;background:#fff;border:1px solid #e2e8f0;border-radius:14px;padding:28px 28px 24px;box-shadow:0 8px 24px rgba(15,23,42,.06)}
                       h1{font-size:1.25rem;margin:0 0 12px} dl{display:grid;grid-template-columns:90px 1fr;gap:4px 12px;margin:16px 0;color:#334155}
                       dt{color:#64748b} dd{margin:0} .hint{color:#475569;font-size:.9rem}
                       button{margin-top:8px;padding:11px 18px;border:0;border-radius:10px;font:600 1rem system-ui;color:#fff;cursor:pointer}
                       .approve{background:#059669} .decline{background:#dc2626}
                       .brand{color:#64748b;font-size:.8rem;margin-top:18px}
                     </style></head>
                     <body><main><h1>{{H(title)}}</h1>{{content}}<div class="brand">AIWorkHub operator</div></main></body></html>
                     """;
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }
}