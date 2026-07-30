namespace NexusHR.Hiring.Application.Abstractions.Security;

public interface IOfferResponseTokenService
{
    GeneratedOfferResponseToken Generate();

    bool Verify(
        string plainTextToken,
        string expectedTokenHash);
}

public sealed record GeneratedOfferResponseToken(
    string PlainText,
    string Hash);