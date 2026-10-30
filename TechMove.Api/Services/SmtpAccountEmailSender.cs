using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

namespace TechMove.Api.Services;

public sealed class SmtpAccountEmailSender(IOptions<EmailDeliveryOptions> options) : IAccountEmailSender
{
    private readonly EmailDeliveryOptions _options = options.Value;

    public Task SendRegistrationConfirmationAsync(
        string email,
        string displayName,
        string confirmationUrl,
        CancellationToken cancellationToken = default)
    {
        var encodedName = HtmlEncoder.Default.Encode(displayName);
        var encodedUrl = HtmlEncoder.Default.Encode(confirmationUrl);
        return SendAsync(
            email,
            "Confirm your TechMove employee registration",
            $"Hello {displayName},\n\nConfirm your TechMove employee registration: {confirmationUrl}\n\nAfter confirmation, a manager will review your account.",
            $"<p>Hello {encodedName},</p><p>Please confirm your TechMove employee registration.</p><p><a href=\"{encodedUrl}\">Confirm email address</a></p><p>After confirmation, a manager will review your account.</p>",
            cancellationToken);
    }

    public Task SendAccountApprovedAsync(
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var encodedName = HtmlEncoder.Default.Encode(displayName);
        return SendAsync(
            email,
            "Your TechMove employee account was approved",
            $"Hello {displayName},\n\nA manager approved your TechMove employee account. You can now sign in.",
            $"<p>Hello {encodedName},</p><p>A manager approved your TechMove employee account.</p><p>You can now sign in to TechMove.</p>",
            cancellationToken);
    }

    private async Task SendAsync(
        string recipient,
        string subject,
        string plainTextBody,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = plainTextBody
        };
        message.To.Add(recipient);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plainTextBody, null, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html"));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
