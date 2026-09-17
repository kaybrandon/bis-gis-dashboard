using System.Net;
using System.Net.Mail;
using GisDashboard.Application.Email;
using Microsoft.Extensions.Logging;

namespace GisDashboard.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IEmailSettingsCache _cache;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IEmailSettingsCache cache, ILogger<SmtpEmailSender> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool IsConfigured => _cache.Current.IsConfigured;

    public async Task<EmailSendResult> SendAsync(OutboundEmail message, CancellationToken cancellationToken = default)
    {
        var settings = _cache.Current;
        if (!settings.IsConfigured)
        {
            _logger.LogInformation(
                "Email dry-run (SMTP is not configured). To={To} Subject={Subject} Attachments={Count}",
                string.Join(", ", message.To),
                message.Subject,
                message.Attachments.Count);
            return new EmailSendResult(false, "dry-run", null);
        }

        var streams = new List<MemoryStream>();
        try
        {
            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30_000
            };
            if (!string.IsNullOrWhiteSpace(settings.User))
            {
                client.Credentials = new NetworkCredential(settings.User, settings.Password);
            }

            using var mail = new MailMessage
            {
                From = new MailAddress(settings.From, settings.FromName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };
            if (!string.IsNullOrWhiteSpace(settings.ReplyTo))
            {
                mail.ReplyToList.Add(new MailAddress(settings.ReplyTo));
            }
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, null, "text/plain"));
            foreach (var to in message.To)
            {
                mail.To.Add(to);
            }

            foreach (var attachment in message.Attachments)
            {
                var stream = new MemoryStream(attachment.Content);
                streams.Add(stream);
                mail.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
            }

            await client.SendMailAsync(mail, cancellationToken);
            return new EmailSendResult(true, "smtp", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed");
            return new EmailSendResult(false, "smtp", ex.Message);
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }
}
