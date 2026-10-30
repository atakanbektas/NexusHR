namespace NexusHR.Candidate.Application.CvAnalyses;

public static class CvFileSignature
{
    public static string? ContentType(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 12) return null;
        var b = bytes.AsSpan();
        if (b[..5].SequenceEqual("%PDF-"u8)) return "application/pdf";
        if (b[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (b[0] == 255 && b[1] == 216 && b[2] == 255) return "image/jpeg";
        if (b[..4].SequenceEqual("RIFF"u8) && b.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
    public static bool Matches(string? name, string? mime, byte[]? bytes) => ContentType(bytes) is { } detected &&
        string.Equals(detected, mime, StringComparison.OrdinalIgnoreCase) && Path.GetExtension(name)?.ToLowerInvariant() switch
        {
            ".pdf" => detected == "application/pdf", ".png" => detected == "image/png",
            ".jpg" or ".jpeg" => detected == "image/jpeg", ".webp" => detected == "image/webp", _ => false
        };
}
