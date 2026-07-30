using System.Security.Cryptography;
using System.Text;
using NexusHR.Hiring.Application.Abstractions.Security;

namespace NexusHR.Hiring.Infrastructure.Security;

public sealed class OfferResponseTokenService
    : IOfferResponseTokenService
{
    private const int TokenByteLength = 32;

    public GeneratedOfferResponseToken Generate()
    {
        var tokenBytes =
            RandomNumberGenerator.GetBytes(
                TokenByteLength);

        var plainTextToken =
            Convert.ToHexString(tokenBytes)
                .ToLowerInvariant();

        var tokenHash =
            ComputeHash(plainTextToken);

        return new GeneratedOfferResponseToken(
            plainTextToken,
            tokenHash);
    }

    public string Hash(string plainTextToken)
    {
        if (string.IsNullOrWhiteSpace(
                plainTextToken))
        {
            throw new ArgumentException(
                "Teklif cevap token değeri boş olamaz.",
                nameof(plainTextToken));
        }

        return ComputeHash(
            plainTextToken.Trim());
    }
    public bool Verify(
        string plainTextToken,
        string expectedTokenHash)
    {
        if (string.IsNullOrWhiteSpace(
                plainTextToken) ||
            string.IsNullOrWhiteSpace(
                expectedTokenHash))
        {
            return false;
        }

        var calculatedHash =
            Hash(plainTextToken);

        try
        {
            var calculatedHashBytes =
                Convert.FromHexString(
                    calculatedHash);

            var expectedHashBytes =
                Convert.FromHexString(
                    expectedTokenHash);

            return CryptographicOperations
                .FixedTimeEquals(
                    calculatedHashBytes,
                    expectedHashBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ComputeHash(
        string plainTextToken)
    {
        var tokenBytes =
            Encoding.UTF8.GetBytes(
                plainTextToken);

        var hashBytes =
            SHA256.HashData(tokenBytes);

        return Convert.ToHexString(hashBytes)
            .ToLowerInvariant();
    }
}