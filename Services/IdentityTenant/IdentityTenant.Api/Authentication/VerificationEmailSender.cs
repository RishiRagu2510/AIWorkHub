using System.Net;
using System.Net.Mail;

namespace IdentityTenant.Api.Authentication;

public interface IVerificationEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string email, string token, CancellationToken ct);
}

public sealed class VerificationEmailSender(IConfiguration config, IHostEnvironment environment)
    : IVerificationEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(config["Smtp:Host"]) &&
                                MailAddress.TryCreate(config["Smtp:From"], out _) &&
                                Uri.TryCreate(config["Frontend:BaseUrl"], UriKind.Absolute, out var uri) &&
                                (uri.Scheme == "https" ||
                                 (environment.IsDevelopment() && uri.IsLoopback && uri.Scheme == "http"));

    public async Task SendAsync(string email, string token, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Verification email delivery is not configured.");
        var endpoint = new Uri(new Uri(config["Frontend:BaseUrl"]!), "/verify-email");
        var url = endpoint.AbsoluteUri + "#token=" + Uri.EscapeDataString(token);
        using var message = new MailMessage(config["Smtp:From"]!, email)
        {
            Subject = "Verify your GRIDHUB company email",
            Body =
                $"Open this link to verify your email and choose your password:\n\n{url}\n\nThis link expires in 30 minutes and can be used once. If you did not request this, ignore this email.",
            IsBodyHtml = false
        };
        using var smtp = new SmtpClient(config["Smtp:Host"], config.GetValue("Smtp:Port", 587))
        {
            EnableSsl = !environment.IsDevelopment() || config.GetValue("Smtp:EnableSsl", true),
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(config["Smtp:Username"])
                ? null
                : new NetworkCredential(config["Smtp:Username"], config["Smtp:Password"])
        };
        await smtp.SendMailAsync(message, ct);
    }
}