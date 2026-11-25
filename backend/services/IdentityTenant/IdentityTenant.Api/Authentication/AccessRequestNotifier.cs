using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using IdentityTenant.Application.Plans;

namespace IdentityTenant.Api.Authentication;

public sealed record DecisionLinks(string ApproveUrl, string DeclineUrl);

public sealed class AccessRequestEmailNotifier(
    IConfiguration config,
    IHostEnvironment environment,
    OperatorActionTokens tokens,
    ILogger<AccessRequestEmailNotifier> logger) : IAccessRequestNotifier
{
    public const string RecipientsKey = "Platform:RequestNotifyEmails";
    public const string BaseUrlKey = "Platform:OperatorBaseUrl";

    private bool MailIsConfigured => !string.IsNullOrWhiteSpace(config["Smtp:Host"]) &&
                                     MailAddress.TryCreate(config["Smtp:From"], out _);

    private Uri? BaseUrl
    {
        get
        {
            var configured = config[BaseUrlKey];
            if (string.IsNullOrWhiteSpace(configured) && environment.IsDevelopment())
                configured = "http://localhost:5233";
            return Uri.TryCreate(configured, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                ? uri
                : null;
        }
    }

    public async Task NotifyAsync(long tenantId, string companyName, AccessRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var to = Recipients();
            if (to.Count == 0 || !MailIsConfigured)
            {
                logger.LogInformation(
                    "Tenant {TenantId} asked for {Item}; no operator email is sent because {Key} or the Smtp settings are not configured.",
                    tenantId, request.Name, RecipientsKey);
                return;
            }

            var links = LinksFor(tenantId, request, DateTimeOffset.UtcNow);
            if (links is null)
                logger.LogWarning(
                    "The operator email for tenant {TenantId} has no Approve/Decline buttons: set Platform:OperatorKey and {Key}.",
                    tenantId, BaseUrlKey);

            using var message = new MailMessage
            {
                From = new MailAddress(config["Smtp:From"]!),
                Subject = Subject(companyName, request),
                Body = Body(tenantId, companyName, request, links)
            };
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                Html(tenantId, companyName, request, links), new ContentType("text/html; charset=utf-8")));
            foreach (var address in to) message.To.Add(address);

            using var smtp = new SmtpClient(config["Smtp:Host"], config.GetValue("Smtp:Port", 587))
            {
                EnableSsl = !environment.IsDevelopment() || config.GetValue("Smtp:EnableSsl", true),
                UseDefaultCredentials = false,
                Timeout = 10_000,
                Credentials = string.IsNullOrWhiteSpace(config["Smtp:Username"])
                    ? null
                    : new NetworkCredential(config["Smtp:Username"], config["Smtp:Password"])
            };
            await smtp.SendMailAsync(message, ct);
            logger.LogInformation("Told the operator that tenant {TenantId} asked for {Item}.", tenantId, request.Name);
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Could not email the operator about tenant {TenantId}'s request for {Item}.",
                tenantId, request.Name);
        }
    }

    public IReadOnlyList<MailAddress> Recipients()
    {
        return (config[RecipientsKey] ?? "")
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(a => MailAddress.TryCreate(a, out var parsed) ? parsed : null)
            .OfType<MailAddress>()
            .ToList();
    }

    public DecisionLinks? LinksFor(long tenantId, AccessRequest request, DateTimeOffset now)
    {
        if (BaseUrl is not { } baseUrl) return null;
        var approve = tokens.Create(tenantId, request.Id, OperatorAction.Approve, now);
        var decline = tokens.Create(tenantId, request.Id, OperatorAction.Decline, now);
        if (approve is null || decline is null) return null;
        return new DecisionLinks(new Uri(baseUrl, $"/operator/access-requests/{approve}").AbsoluteUri,
            new Uri(baseUrl, $"/operator/access-requests/{decline}").AbsoluteUri);
    }


    private static string OneLine(string text)
    {
        return string.Join(' ',
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static string Subject(string companyName, AccessRequest request)
    {
        return request.IsCancellation
            ? $"AIWorkHub cancellation request: {OneLine(companyName)} wants to cancel {TenantAccessRequests.DisplayName(request.Kind, request.Name)}"
            : $"AIWorkHub access request: {OneLine(companyName)} wants {TenantAccessRequests.DisplayName(request.Kind, request.Name)}";
    }

    private static string Ask(AccessRequest request)
    {
        return request.IsCancellation ? "to cancel" : "for";
    }

    private static string When(AccessRequest request)
    {
        return request.CreatedAt.UtcDateTime.ToString("dd MMM yyyy HH:mm",
            CultureInfo.InvariantCulture) + " UTC";
    }


    public static string Body(long tenantId, string companyName, AccessRequest request, DecisionLinks? links)
    {
        var what = TenantAccessRequests.DisplayName(request.Kind, request.Name);
        var text = new StringBuilder()
            .AppendLine($"{OneLine(companyName)} (tenant {tenantId}) has asked {Ask(request)} {what}.")
            .AppendLine()
            .AppendLine($"Asked by: {OneLine(request.UserName)} (user {request.UserId})")
            .AppendLine($"When:     {When(request)}")
            .AppendLine($"Note:     {(string.IsNullOrWhiteSpace(request.Note) ? "(none)" : request.Note)}")
            .AppendLine();
        if (links is null)
            text.AppendLine(
                "Nothing has changed yet. This message has no Approve/Decline links because the operator key or Platform:OperatorBaseUrl is not set; use the operator API to decide it.");
        else
            text.AppendLine("Nothing changes until you confirm on the page that opens.")
                .AppendLine()
                .AppendLine($"{(request.IsCancellation ? "Approve cancellation" : "Approve")}: {links.ApproveUrl}")
                .AppendLine($"{(request.IsCancellation ? "Keep their plan" : "Decline")}: {links.DeclineUrl}")
                .AppendLine()
                .AppendLine("The links work for 7 days.");
        return text.ToString();
    }


    public static string Html(long tenantId, string companyName, AccessRequest request, DecisionLinks? links)
    {
        static string H(string? text)
        {
            return WebUtility.HtmlEncode(text ?? "");
        }

        var what = TenantAccessRequests.DisplayName(request.Kind, request.Name);
        var buttons = links is null
            ? "<p style=\"color:#92400e\">There are no Approve/Decline buttons because the operator key or <code>Platform:OperatorBaseUrl</code> is not set. Use the operator API to decide this request.</p>"
            : $"""
               <p style="margin:20px 0">
                 <a href="{H(links.ApproveUrl)}" style="background:#059669;color:#ffffff;text-decoration:none;font-weight:600;padding:11px 22px;border-radius:8px;display:inline-block;margin-right:10px">{(request.IsCancellation ? "Approve cancellation" : "Approve")}</a>
                 <a href="{H(links.DeclineUrl)}" style="background:#dc2626;color:#ffffff;text-decoration:none;font-weight:600;padding:11px 22px;border-radius:8px;display:inline-block">{(request.IsCancellation ? "Keep their plan" : "Decline")}</a>
               </p>
               <p style="color:#64748b;font-size:13px">Nothing changes until you confirm on the page that opens. The buttons work for 7 days.</p>
               """;
        return $"""
                <div style="font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;font-size:15px;color:#0f172a;max-width:560px">
                  <p><strong>{H(OneLine(companyName))}</strong> (tenant {tenantId}) has asked {Ask(request)} <strong>{H(what)}</strong>.</p>
                  <table style="border-collapse:collapse;font-size:14px">
                    <tr><td style="color:#64748b;padding:2px 14px 2px 0">Asked by</td><td>{H(OneLine(request.UserName))} (user {request.UserId})</td></tr>
                    <tr><td style="color:#64748b;padding:2px 14px 2px 0">When</td><td>{H(When(request))}</td></tr>
                    <tr><td style="color:#64748b;padding:2px 14px 2px 0;vertical-align:top">Note</td><td>{(string.IsNullOrWhiteSpace(request.Note) ? "(none)" : H(request.Note))}</td></tr>
                  </table>
                  {buttons}
                </div>
                """;
    }
}