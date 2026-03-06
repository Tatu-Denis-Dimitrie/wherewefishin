using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Configuration;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.Core.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public SmtpEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string? firstName)
    {
        var host = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("SMTP host is not configured.");

        var port = int.TryParse(_configuration["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        var enableSsl = !bool.TryParse(_configuration["Smtp:EnableSsl"], out var parsedEnableSsl) || parsedEnableSsl;
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"] ?? username;
        var fromName = _configuration["Smtp:FromName"] ?? "WhereWeFishin";
        var frontendUrl = NormalizeUrl(_configuration["Smtp:FrontendUrl"]
            ?? _configuration["Frontend:Url"]
            ?? "http://localhost:4200");

        if (string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("SMTP from address is not configured.");

        var plainTextBody = BuildWelcomePlainTextBody(firstName, frontendUrl);
        var htmlBody = BuildWelcomeHtmlBody(firstName, frontendUrl);

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Welcome to WhereWeFishin",
            Body = plainTextBody,
            IsBodyHtml = false
        };

        message.To.Add(toEmail);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl
        };

        var useDefaultCredentials = bool.TryParse(_configuration["Smtp:UseDefaultCredentials"], out var parsedUseDefaultCredentials)
            && parsedUseDefaultCredentials;

        client.UseDefaultCredentials = useDefaultCredentials;

        if (!useDefaultCredentials && !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        await client.SendMailAsync(message);
    }

        private static string BuildWelcomePlainTextBody(string? firstName, string frontendUrl)
    {
        var safeFirstName = string.IsNullOrWhiteSpace(firstName) ? "angler" : firstName.Trim();

        return $"Hello {safeFirstName},\n\n" +
               "Welcome to WhereWeFishin! Your account is now active and ready to use.\n" +
               "You can now sign in, discover fishing spots, and track your catches.\n\n" +
                             $"Open app: {frontendUrl}\n\n" +
               "Tight lines,\n" +
               "WhereWeFishin Team";
    }

        private static string BuildWelcomeHtmlBody(string? firstName, string frontendUrl)
        {
                var safeFirstName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(firstName) ? "angler" : firstName.Trim());
                var safeFrontendUrl = WebUtility.HtmlEncode(frontendUrl);

                return $"""
<!doctype html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Welcome to WhereWeFishin</title>
</head>
<body style="margin:0;padding:0;background-color:#edf2f7;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
    <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background-color:#edf2f7;">
        <tr>
            <td align="center" style="padding:24px 12px;">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="600" style="width:100%;max-width:600px;background-color:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 8px 28px rgba(15,23,42,0.12);">
                    <tr>
                        <td style="background-color:#0b3c5d;padding:28px 32px;text-align:center;">
                            <p style="margin:0 0 8px 0;font-size:11px;line-height:16px;letter-spacing:2px;text-transform:uppercase;color:#c7def0;">WELCOME ABOARD</p>
                            <h1 style="margin:0;font-size:30px;line-height:36px;font-weight:800;color:#ffffff;">WhereWeFishin</h1>
                            <p style="margin:10px 0 0 0;font-size:14px;line-height:20px;color:#d9e8f4;">Your fishing journey starts here.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style="padding:30px 32px 20px 32px;">
                            <p style="margin:0 0 14px 0;font-size:16px;line-height:24px;color:#0f172a;">Hi {safeFirstName},</p>
                            <p style="margin:0 0 14px 0;font-size:15px;line-height:24px;color:#334155;">Your account has been created successfully. You can now discover fishing spots, track your catches, and build your personal fishing timeline.</p>
                            <p style="margin:0 0 24px 0;font-size:15px;line-height:24px;color:#334155;">We are glad to have you in the community.</p>

                            <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                                <tr>
                                    <td style="border-radius:8px;background-color:#0b6e99;">
                                        <a href="{safeFrontendUrl}" style="display:inline-block;padding:12px 22px;font-size:15px;line-height:20px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:8px;">Open WhereWeFishin</a>
                                    </td>
                                </tr>
                            </table>

                            <p style="margin:24px 0 0 0;font-size:13px;line-height:20px;color:#64748b;">If the button does not work, copy this link into your browser:<br><a href="{safeFrontendUrl}" style="color:#0b6e99;text-decoration:none;">{safeFrontendUrl}</a></p>
                        </td>
                    </tr>
                    <tr>
                        <td style="padding:18px 32px;background-color:#f8fafc;border-top:1px solid #e2e8f0;">
                            <p style="margin:0;font-size:12px;line-height:18px;color:#64748b;">Tight lines,<br>WhereWeFishin Team</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
""";
        }

        private static string NormalizeUrl(string frontendUrl)
        {
                if (string.IsNullOrWhiteSpace(frontendUrl))
                        return "http://localhost:4200";

                return frontendUrl.Trim().TrimEnd('/');
        }
}
