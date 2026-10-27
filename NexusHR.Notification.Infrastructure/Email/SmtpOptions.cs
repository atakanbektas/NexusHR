namespace NexusHR.Notification.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } =
        string.Empty;

    public int Port { get; init; } = 1025;

    public string SenderEmail { get; init; } =
        string.Empty;

    public string SenderName { get; init; } =
        string.Empty;

    public string? Username { get; init; }

    public string? Password { get; init; }

    public bool UseSsl { get; init; }
}