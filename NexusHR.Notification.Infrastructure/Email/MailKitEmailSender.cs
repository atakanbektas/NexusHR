using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NexusHR.Notification.Application.Abstractions.Email;
using NexusHR.Notification.Application.Emails;

namespace NexusHR.Notification.Infrastructure.Email;

internal sealed class MailKitEmailSender(
    IOptions<SmtpOptions> options)
    : IEmailSender
{
    private static readonly System.Diagnostics.ActivitySource Telemetry = new("NexusHR.Business");
    private readonly SmtpOptions _options =
        options.Value;

    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        using var activity=Telemetry.StartActivity("SMTP send",System.Diagnostics.ActivityKind.Client);
        activity?.SetTag("nexushr.operation","smtp-send");
        try {await SendCoreAsync(message,cancellationToken);}
        catch(Exception error) {activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);activity?.SetTag("error.type",error.GetType().FullName);throw;}
    }
    private async Task SendCoreAsync(EmailMessage message,CancellationToken cancellationToken)
    {
        var email = new MimeMessage();

        email.From.Add(
            new MailboxAddress(
                _options.SenderName,
                _options.SenderEmail));

        email.To.Add(
            new MailboxAddress(
                message.RecipientName,
                message.RecipientEmail));

        email.Subject = message.Subject;

        email.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody
        }.ToMessageBody();

        using var smtpClient =
            new SmtpClient();

        var socketOptions =
            _options.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.None;

        await smtpClient.ConnectAsync(
            _options.Host,
            _options.Port,
            socketOptions,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(
                _options.Username))
        {
            await smtpClient.AuthenticateAsync(
                _options.Username,
                _options.Password ?? string.Empty,
                cancellationToken);
        }

        await smtpClient.SendAsync(
            email,
            cancellationToken);

        await smtpClient.DisconnectAsync(
            true,
            cancellationToken);
    }
}
