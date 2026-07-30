namespace NexusHR.Notification.Application.Emails;

public sealed record EmailMessage(
    string RecipientEmail,
    string RecipientName,
    string Subject,
    string HtmlBody);