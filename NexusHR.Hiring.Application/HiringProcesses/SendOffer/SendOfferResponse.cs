namespace NexusHR.Hiring.Application.HiringProcesses.SendOffer;

public sealed record SendOfferResponse(
    bool IsSuccess,
    string? ErrorCode,
    IReadOnlyCollection<string> Errors)
{
    public static SendOfferResponse Success()
    {
        return new SendOfferResponse(
            IsSuccess: true,
            ErrorCode: null,
            Errors: Array.Empty<string>());
    }

    public static SendOfferResponse Failure(
        string errorCode,
        params string[] errors)
    {
        return new SendOfferResponse(
            IsSuccess: false,
            ErrorCode: errorCode,
            Errors: errors);
    }
}