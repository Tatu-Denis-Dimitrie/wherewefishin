using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Configuration;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.Core.Services;

public class SmtpEmailService : IEmailService
{
    private static readonly CultureInfo RoCulture = CultureInfo.GetCultureInfo("ro-RO");
    private readonly IConfiguration _configuration;

    public SmtpEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string? firstName)
    {
        var smtpSettings = GetSmtpSettings();
        var frontendUrl = GetFrontendUrl();

        var plainTextBody = BuildWelcomePlainTextBody(firstName, frontendUrl);
        var htmlBody = BuildWelcomeHtmlBody(firstName, frontendUrl);

        await SendEmailAsync(toEmail, "Welcome to WhereWeFishin", plainTextBody, htmlBody, smtpSettings);
    }

    public async Task SendBookingConfirmationEmailAsync(
        string toEmail,
        string? firstName,
        string spotName,
        DateTime startDateUtc,
        int durationHours,
        decimal totalPrice,
        int bookingId)
    {
        var smtpSettings = GetSmtpSettings();
        var frontendUrl = GetFrontendUrl();

        var plainTextBody = BuildBookingPlainTextBody(firstName, spotName, startDateUtc, durationHours, totalPrice, bookingId, frontendUrl);
        var htmlBody = BuildBookingHtmlBody(firstName, spotName, startDateUtc, durationHours, totalPrice, bookingId, frontendUrl);

        await SendEmailAsync(toEmail, $"Booking Confirmation #{bookingId} - WhereWeFishin", plainTextBody, htmlBody, smtpSettings);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string? firstName, string resetCode)
    {
        var smtpSettings = GetSmtpSettings();
        var plainTextBody = BuildPasswordResetPlainTextBody(firstName, resetCode);
        var htmlBody = BuildPasswordResetHtmlBody(firstName, resetCode);
        await SendEmailAsync(toEmail, "Password Reset - WhereWeFishin", plainTextBody, htmlBody, smtpSettings);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string plainTextBody, string htmlBody, SmtpSettings settings)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return;

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = subject,
            Body = plainTextBody,
            IsBodyHtml = false
        };

        message.To.Add(toEmail);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            UseDefaultCredentials = settings.UseDefaultCredentials
        };

        if (!settings.UseDefaultCredentials && !string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrWhiteSpace(settings.Password))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
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

    private static string BuildBookingPlainTextBody(
        string? firstName,
        string spotName,
        DateTime startDateUtc,
        int durationHours,
        decimal totalPrice,
        int bookingId,
        string frontendUrl)
    {
        var safeFirstName = string.IsNullOrWhiteSpace(firstName) ? "angler" : firstName.Trim();
        var startDateText = startDateUtc.ToString("dd.MM.yyyy HH:mm", RoCulture) + " UTC";
        var priceText = totalPrice.ToString("N2", RoCulture) + " RON";

        return $"Hello {safeFirstName},\n\n" +
               "Your fishing session booking has been confirmed.\n\n" +
               $"Booking ID: #{bookingId}\n" +
               $"Location: {spotName}\n" +
               $"Start: {startDateText}\n" +
               $"Duration: {durationHours} hours\n" +
               $"Total paid: {priceText}\n\n" +
               $"View your bookings: {frontendUrl}/bookings\n\n" +
               "Tight lines,\n" +
               "WhereWeFishin Team";
    }

    private static string BuildBookingHtmlBody(
        string? firstName,
        string spotName,
        DateTime startDateUtc,
        int durationHours,
        decimal totalPrice,
        int bookingId,
        string frontendUrl)
    {
        var safeFirstName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(firstName) ? "angler" : firstName.Trim());
        var safeSpotName = WebUtility.HtmlEncode(spotName);
        var startDateText = WebUtility.HtmlEncode(startDateUtc.ToString("dd.MM.yyyy HH:mm", RoCulture) + " UTC");
        var durationText = WebUtility.HtmlEncode(durationHours.ToString(RoCulture) + " hours");
        var priceText = WebUtility.HtmlEncode(totalPrice.ToString("N2", RoCulture) + " RON");
        var safeFrontendUrl = WebUtility.HtmlEncode(frontendUrl);
        var bookingsUrl = WebUtility.HtmlEncode($"{frontendUrl}/bookings");

        return $"""
<!doctype html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Fishing session booking confirmation</title>
</head>
<body style="margin:0;padding:0;background-color:#eaf4f7;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
    <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:linear-gradient(180deg,#d7edf5 0%,#eaf4f7 100%);">
        <tr>
            <td align="center" style="padding:24px 12px;">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="600" style="width:100%;max-width:600px;background-color:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 10px 30px rgba(15,23,42,0.14);">
                    <tr>
                        <td style="background:linear-gradient(135deg,#0a3a54 0%,#0d5a7f 100%);padding:28px 32px;text-align:center;">
                            <p style="margin:0;font-size:12px;line-height:18px;letter-spacing:1.5px;color:#cae7f6;text-transform:uppercase;">Payment Confirmed</p>
                            <h1 style="margin:8px 0 0 0;font-size:30px;line-height:36px;color:#ffffff;font-weight:800;">Session Confirmed</h1>
                            <p style="margin:10px 0 0 0;font-size:14px;line-height:21px;color:#def0fa;">Booking #{bookingId} is active.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style="padding:28px 32px 18px 32px;">
                            <p style="margin:0 0 14px 0;font-size:16px;line-height:24px;color:#0f172a;">Hello {safeFirstName},</p>
                            <p style="margin:0 0 18px 0;font-size:15px;line-height:24px;color:#334155;">Your payment has been recorded. Here are the details of your fishing session:</p>

                            <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="border:1px solid #dbe7ef;border-radius:10px;overflow:hidden;background-color:#f8fbfd;">
                                <tr>
                                    <td style="padding:11px 14px;font-size:13px;color:#475569;border-bottom:1px solid #dbe7ef;width:38%;">Location</td>
                                    <td style="padding:11px 14px;font-size:13px;color:#0f172a;border-bottom:1px solid #dbe7ef;font-weight:600;">{safeSpotName}</td>
                                </tr>
                                <tr>
                                    <td style="padding:11px 14px;font-size:13px;color:#475569;border-bottom:1px solid #dbe7ef;">Start date</td>
                                    <td style="padding:11px 14px;font-size:13px;color:#0f172a;border-bottom:1px solid #dbe7ef;font-weight:600;">{startDateText}</td>
                                </tr>
                                <tr>
                                    <td style="padding:11px 14px;font-size:13px;color:#475569;border-bottom:1px solid #dbe7ef;">Duration</td>
                                    <td style="padding:11px 14px;font-size:13px;color:#0f172a;border-bottom:1px solid #dbe7ef;font-weight:600;">{durationText}</td>
                                </tr>
                                <tr>
                                    <td style="padding:11px 14px;font-size:13px;color:#475569;">Total paid</td>
                                    <td style="padding:11px 14px;font-size:15px;color:#0b6e99;font-weight:800;">{priceText}</td>
                                </tr>
                            </table>

                            <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin-top:22px;">
                                <tr>
                                    <td style="border-radius:8px;background-color:#0b6e99;">
                                        <a href="{bookingsUrl}" style="display:inline-block;padding:12px 22px;font-size:15px;line-height:20px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:8px;">View my bookings</a>
                                    </td>
                                </tr>
                            </table>

                            <p style="margin:22px 0 0 0;font-size:13px;line-height:20px;color:#64748b;">If the button doesn't work, copy this link into your browser:<br><a href="{bookingsUrl}" style="color:#0b6e99;text-decoration:none;">{bookingsUrl}</a></p>
                        </td>
                    </tr>
                    <tr>
                        <td style="padding:18px 32px;background-color:#f5fafc;border-top:1px solid #e2e8f0;">
                            <p style="margin:0;font-size:12px;line-height:18px;color:#64748b;">Tight lines,<br>WhereWeFishin Team</p>
                            <p style="margin:6px 0 0 0;font-size:12px;line-height:18px;color:#94a3b8;">Official platform: <a href="{safeFrontendUrl}" style="color:#0b6e99;text-decoration:none;">{safeFrontendUrl}</a></p>
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

    private static string BuildPasswordResetPlainTextBody(string? firstName, string resetCode)
    {
        var safeFirstName = string.IsNullOrWhiteSpace(firstName) ? "angler" : firstName.Trim();

        return $"Hello {safeFirstName},\n\n" +
               "You requested a password reset for your WhereWeFishin account.\n\n" +
               $"Your verification code is: {resetCode}\n\n" +
               "The code is valid for 15 minutes. If you did not request this, please ignore this email.\n\n" +
               "Tight lines,\n" +
               "WhereWeFishin Team";
    }

    private static string BuildPasswordResetHtmlBody(string? firstName, string resetCode)
    {
        var safeFirstName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(firstName) ? "angler" : firstName.Trim());
        var safeCode = WebUtility.HtmlEncode(resetCode);

        return $"""
<!doctype html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>WhereWeFishin Password Reset</title>
</head>
<body style="margin:0;padding:0;background-color:#edf2f7;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
    <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background-color:#edf2f7;">
        <tr>
            <td align="center" style="padding:24px 12px;">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="600" style="width:100%;max-width:600px;background-color:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 8px 28px rgba(15,23,42,0.12);">
                    <tr>
                        <td style="background-color:#0b3c5d;padding:28px 32px;text-align:center;">
                            <p style="margin:0 0 8px 0;font-size:11px;line-height:16px;letter-spacing:2px;text-transform:uppercase;color:#c7def0;">ACCOUNT SECURITY</p>
                            <h1 style="margin:0;font-size:30px;line-height:36px;font-weight:800;color:#ffffff;">WhereWeFishin</h1>
                            <p style="margin:10px 0 0 0;font-size:14px;line-height:20px;color:#d9e8f4;">Password reset</p>
                        </td>
                    </tr>
                    <tr>
                        <td style="padding:30px 32px 20px 32px;">
                            <p style="margin:0 0 14px 0;font-size:16px;line-height:24px;color:#0f172a;">Hello {safeFirstName},</p>
                            <p style="margin:0 0 14px 0;font-size:15px;line-height:24px;color:#334155;">You requested a password reset for your account. Use the code below to set a new password.</p>
                            <p style="margin:0 0 24px 0;font-size:15px;line-height:24px;color:#334155;">Your verification code (valid for <strong>15 minutes</strong>):</p>

                            <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%">
                                <tr>
                                    <td align="center" style="padding:18px;background-color:#f0f7ff;border-radius:10px;border:1px solid #bfdbfe;">
                                        <span style="font-size:36px;font-weight:800;letter-spacing:10px;color:#0b3c5d;font-family:monospace;">{safeCode}</span>
                                    </td>
                                </tr>
                            </table>

                            <p style="margin:24px 0 0 0;font-size:13px;line-height:20px;color:#64748b;">If you did not request this reset, ignore this email. Your password will not be changed.</p>
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

    private SmtpSettings GetSmtpSettings()
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
        var useDefaultCredentials = bool.TryParse(_configuration["Smtp:UseDefaultCredentials"], out var parsedUseDefaultCredentials)
            && parsedUseDefaultCredentials;

        if (string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("SMTP from address is not configured.");

        return new SmtpSettings(host, port, enableSsl, useDefaultCredentials, username, password, fromEmail, fromName);
    }

    private string GetFrontendUrl()
    {
        return NormalizeUrl(_configuration["Smtp:FrontendUrl"]
            ?? _configuration["Frontend:Url"]
            ?? "http://localhost:4200");
    }

    private static string NormalizeUrl(string frontendUrl)
    {
        if (string.IsNullOrWhiteSpace(frontendUrl))
            return "http://localhost:4200";

        return frontendUrl.Trim().TrimEnd('/');
    }

    private sealed record SmtpSettings(
        string Host,
        int Port,
        bool EnableSsl,
        bool UseDefaultCredentials,
        string? Username,
        string? Password,
        string FromEmail,
        string FromName);
}
