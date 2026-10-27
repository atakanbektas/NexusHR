namespace NexusHR.Hiring.Domain.HiringProcesses;

public enum HiringProcessStatus
{
    Draft = 1,
    OfferPrepared = 2,
    OfferSent = 3,
    OfferAccepted = 4,
    OfferRejected = 5,
    Cancelled = 6,
    Completed = 7
}