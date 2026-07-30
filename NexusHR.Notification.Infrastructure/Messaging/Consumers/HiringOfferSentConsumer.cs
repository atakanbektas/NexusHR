using System.Globalization;
using System.Net;
using MassTransit;
using Microsoft.Extensions.Configuration;
using NexusHR.Contracts.Hiring;
using NexusHR.Notification.Application.Abstractions.Email;
using NexusHR.Notification.Application.Emails;

namespace NexusHR.Notification.Infrastructure
    .Messaging.Consumers;

internal sealed class HiringOfferSentConsumer(
    IEmailSender emailSender,
    IConfiguration configuration)
    : IConsumer<
        HiringOfferSentIntegrationEvent>
{
    public async Task Consume(
        ConsumeContext<
            HiringOfferSentIntegrationEvent> context)
    {
        var integrationEvent =
            context.Message;

        if (integrationEvent.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                "Desteklenmeyen HiringOfferSent event sürümü.");
        }

        var publicWebUrl =
            configuration[
                "OfferResponse:PublicWebUrl"]
            ?? throw new InvalidOperationException(
                "OfferResponse:PublicWebUrl ayarı bulunamadı.");

        var responseUrl =
            $"{publicWebUrl.TrimEnd('/')}" +
            "/public/offers/respond" +
            $"?token={Uri.EscapeDataString(
                integrationEvent.ResponseToken)}";

        var candidateFullName =
            $"{integrationEvent.CandidateFirstName} " +
            integrationEvent.CandidateLastName;

        var formattedSalary =
            integrationEvent.GrossSalary
                .ToString(
                    "N2",
                    CultureInfo.GetCultureInfo(
                        "tr-TR"));

        var htmlBody = CreateHtmlBody(
            candidateFullName,
            integrationEvent.PositionTitle,
            integrationEvent.Department,
            formattedSalary,
            integrationEvent.Currency,
            integrationEvent.ProposedStartDate,
            integrationEvent.OfferExpiresAtUtc,
            responseUrl);

        var emailMessage =
            new EmailMessage(
                RecipientEmail:
                    integrationEvent.CandidateEmail,
                RecipientName:
                    candidateFullName,
                Subject:
                    $"NexusHR iş teklifi - {integrationEvent.PositionTitle}",
                HtmlBody:
                    htmlBody);

        await emailSender.SendAsync(
            emailMessage,
            context.CancellationToken);
    }

    private static string CreateHtmlBody(
        string candidateFullName,
        string positionTitle,
        string department,
        string formattedSalary,
        string currency,
        DateOnly proposedStartDate,
        DateTime offerExpiresAtUtc,
        string responseUrl)
    {
        var encodedCandidateName =
            WebUtility.HtmlEncode(
                candidateFullName);

        var encodedPositionTitle =
            WebUtility.HtmlEncode(
                positionTitle);

        var encodedDepartment =
            WebUtility.HtmlEncode(
                department);

        var encodedResponseUrl =
            WebUtility.HtmlEncode(
                responseUrl);

        return $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
            <meta charset="utf-8">
        </head>
        <body style="font-family:Arial,sans-serif;background:#f4f7fb;padding:30px;">
            <div style="max-width:620px;margin:auto;background:#ffffff;border-radius:12px;padding:32px;">
                <h1 style="color:#172554;">NexusHR İş Teklifi</h1>

                <p>Merhaba {{encodedCandidateName}},</p>

                <p>
                    Sizi aşağıdaki pozisyon için ekibimizde görmek istiyoruz.
                </p>

                <table style="width:100%;border-collapse:collapse;margin:24px 0;">
                    <tr>
                        <td style="padding:8px;font-weight:bold;">Pozisyon</td>
                        <td style="padding:8px;">{{encodedPositionTitle}}</td>
                    </tr>
                    <tr>
                        <td style="padding:8px;font-weight:bold;">Departman</td>
                        <td style="padding:8px;">{{encodedDepartment}}</td>
                    </tr>
                    <tr>
                        <td style="padding:8px;font-weight:bold;">Aylık brüt ücret</td>
                        <td style="padding:8px;">{{formattedSalary}} {{currency}}</td>
                    </tr>
                    <tr>
                        <td style="padding:8px;font-weight:bold;">Başlangıç tarihi</td>
                        <td style="padding:8px;">{{proposedStartDate:dd.MM.yyyy}}</td>
                    </tr>
                    <tr>
                        <td style="padding:8px;font-weight:bold;">Son cevap zamanı</td>
                        <td style="padding:8px;">{{offerExpiresAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}}</td>
                    </tr>
                </table>

                <a
                    href="{{encodedResponseUrl}}"
                    style="display:inline-block;background:#2563eb;color:#ffffff;padding:14px 22px;text-decoration:none;border-radius:8px;font-weight:bold;">
                    Teklifi görüntüle
                </a>

                <p style="margin-top:24px;color:#64748b;font-size:13px;">
                    Bu bağlantı kişiye özeldir. Başka kişilerle paylaşmayınız.
                </p>
            </div>
        </body>
        </html>
        """;
    }
}