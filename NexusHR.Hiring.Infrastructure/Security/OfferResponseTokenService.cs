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
            ComputeHash(plainTextToken);

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