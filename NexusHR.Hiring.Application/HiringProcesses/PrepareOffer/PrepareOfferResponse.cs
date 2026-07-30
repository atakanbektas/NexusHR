namespace NexusHR.Hiring.Application.HiringProcesses.PrepareOffer;

public sealed record PrepareOfferResponse(
    bool IsSuccess,
    string? ErrorCode,
    IReadOnlyCollection<string> Errors)
{
    public static PrepareOfferResponse Success()
    {
        return new PrepareOfferResponse(
            IsSuccess: true,
            ErrorCode: null,
            Errors: Array.Empty<string>());
    }

    public static PrepareOfferResponse Failure(
        string errorCode,
        params string[] errors)
    {
        return new PrepareOfferResponse(
            IsSuccess: false,
            ErrorCode: errorCode,
            Errors: errors);
    }
}