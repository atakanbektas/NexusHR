namespace NexusHR.Hiring.Application.HiringProcesses
    .GetActiveHiringProcessByCandidateId;

public sealed record ActiveHiringProcessLookupResponse(
    bool Exists,
    Guid? HiringProcessId,
    string? Status)
{
    public static ActiveHiringProcessLookupResponse NotFound()
    {
        return new ActiveHiringProcessLookupResponse(
            Exists: false,
            HiringProcessId: null,
            Status: null);
    }

    public static ActiveHiringProcessLookupResponse Found(
        Guid hiringProcessId,
        string status)
    {
        return new ActiveHiringProcessLookupResponse(
            Exists: true,
            HiringProcessId: hiringProcessId,
            Status: status);
    }
}