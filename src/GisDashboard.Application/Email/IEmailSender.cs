namespace GisDashboard.Application.Email;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record OutboundEmail(
    IReadOnlyList<string> To,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailAttachment> Attachments);

public sealed record EmailSendResult(bool Delivered, string Mode, string? Error);

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task<EmailSendResult> SendAsync(OutboundEmail message, CancellationToken cancellationToken = default);
}
