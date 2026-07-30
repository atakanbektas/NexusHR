namespace NexusHR.Hiring.Application
    .HiringProcesses.RespondToOffer;

public sealed record RespondToOfferResponse(
    bool IsSuccess,
    string? ErrorCode,
    IReadOnlyCollection<string> Errors)
{
    public static RespondToOfferResponse Success()
    {
        return new RespondToOfferResponse(
            IsSuccess: true,
            ErrorCode: null,
            Errors: Array.Empty<string>());
    }

    public static RespondToOfferResponse Failure(
        string errorCode,
        params string[] errors)
    {
        return new RespondToOfferResponse(
            IsSuccess: false,
            ErrorCode: errorCode,
            Errors: errors);
    }
}