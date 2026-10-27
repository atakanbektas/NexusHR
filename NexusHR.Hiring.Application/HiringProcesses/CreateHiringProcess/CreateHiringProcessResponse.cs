namespace NexusHR.Hiring.Application.HiringProcesses.CreateHiringProcess;

public sealed record CreateHiringProcessResponse(
    bool IsSuccess,
    Guid? HiringProcessId,
    string? ErrorCode,
    IReadOnlyCollection<string> Errors)
{
    public static CreateHiringProcessResponse Success(
        Guid hiringProcessId)
    {
        return new CreateHiringProcessResponse(
            IsSuccess: true,
            HiringProcessId: hiringProcessId,
            ErrorCode: null,
            Errors: Array.Empty<string>());
    }

    public static CreateHiringProcessResponse Failure(
        string errorCode,
        params string[] errors)
    {
        return new CreateHiringProcessResponse(
            IsSuccess: false,
            HiringProcessId: null,
            ErrorCode: errorCode,
            Errors: errors);
    }
}