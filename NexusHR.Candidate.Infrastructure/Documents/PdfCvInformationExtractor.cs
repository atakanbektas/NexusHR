using System.Text.RegularExpressions;
using NexusHR.Candidate.Application.Abstractions.Documents;

namespace NexusHR.Candidate.Infrastructure.Documents;

internal sealed class PdfCvInformationExtractor(NexusHR.Candidate.Application.CvAnalyses.ICvTextExtractor textExtractor)
    : ICvInformationExtractor
{
    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex PhoneRegex = new(
        @"(?:\+?90|0)?\s*\(?5\d{2}\)?[\s.-]*\d{3}[\s.-]*\d{2}[\s.-]*\d{2}",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public async Task<ExtractedCandidateDraft> ExtractAsync(
        byte[] fileContent,
        CancellationToken cancellationToken)
    {
        var pages = await textExtractor.ExtractPagesAsync(fileContent, cancellationToken);
        var text = string.Concat(pages.Select(page => page.Text + Environment.NewLine));

        var email = ExtractEmail(text);
        var phoneNumber = ExtractPhoneNumber(text);
        var (firstName, lastName) = ExtractName(text);

        var draft = new ExtractedCandidateDraft(
            firstName,
            lastName,
            email,
            phoneNumber);

        return draft;
    }

    private static string? ExtractEmail(string text)
    {
        var match = EmailRegex.Match(text);

        return match.Success
            ? match.Value.Trim().ToLowerInvariant()
            : null;
    }

    private static string? ExtractPhoneNumber(string text)
    {
        var match = PhoneRegex.Match(text);

        if (!match.Success)
        {
            return null;
        }

        var digits = new string(
            match.Value
                .Where(char.IsDigit)
                .ToArray());

        if (digits.StartsWith("90") && digits.Length == 12)
        {
            digits = $"0{digits[2..]}";
        }
        else if (digits.StartsWith('5') && digits.Length == 10)
        {
            digits = $"0{digits}";
        }

        return digits;
    }

    private static (string? FirstName, string? LastName) ExtractName(
        string text)
    {
        var ignoredWords = new[]
        {
            "cv",
            "özgeçmiş",
            "curriculum",
            "vitae",
            "telefon",
            "phone",
            "e-posta",
            "email",
            "linkedin"
        };

        var lines = text
            .Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length is >= 3 and <= 80)
            .ToArray();

        foreach (var line in lines)
        {
            if (ignoredWords.Any(word =>
                    line.Contains(
                        word,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (line.Contains('@') || line.Any(char.IsDigit))
            {
                continue;
            }

            var parts = line
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length is < 2 or > 4)
            {
                continue;
            }

            if (parts.Any(part =>
                    part.Any(character =>
                        !char.IsLetter(character) &&
                        character != '-')))
            {
                continue;
            }

            var firstName = string.Join(
                " ",
                parts[..^1]);

            var lastName = parts[^1];

            return (firstName, lastName);
        }

        return (null, null);
    }
}
