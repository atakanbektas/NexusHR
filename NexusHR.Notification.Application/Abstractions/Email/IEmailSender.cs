using NexusHR.Notification.Application.Emails;

namespace NexusHR.Notification.Application.Abstractions.Email;

public interface IEmailSender
{
    Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken);
}