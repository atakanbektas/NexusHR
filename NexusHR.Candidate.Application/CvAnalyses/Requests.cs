using MediatR;

namespace NexusHR.Candidate.Application.CvAnalyses;

public sealed record RequestCvAnalysisCommand(Guid CandidateId, Guid DocumentId, string User, string Key, bool Consent) : IRequest<AnalysisView>;
public sealed record ListCvAnalysesQuery(Guid CandidateId, int Page = 1) : IRequest<AnalysisHistory>;
public sealed record GetCvAnalysisQuery(Guid CandidateId, Guid Id) : IRequest<AnalysisView>;
public sealed record ChangeCvAnalysisCommand(Guid CandidateId, Guid Id, string User, uint Version, string Action,
    bool AcknowledgeUnknown = false, string? Decision = null, string? Notes = null, int? FactIndex = null, string? CorrectedValue = null) : IRequest<AnalysisView>;

internal sealed class AnalysisHandlers(ICvAnalysisStore store) :
    IRequestHandler<RequestCvAnalysisCommand, AnalysisView>, IRequestHandler<ListCvAnalysesQuery, AnalysisHistory>,
    IRequestHandler<GetCvAnalysisQuery, AnalysisView>, IRequestHandler<ChangeCvAnalysisCommand, AnalysisView>
{
    public Task<AnalysisView> Handle(RequestCvAnalysisCommand r, CancellationToken ct) =>
        store.RequestAsync(r.CandidateId, r.DocumentId, r.User, r.Key, r.Consent, ct);
    public Task<AnalysisHistory> Handle(ListCvAnalysesQuery r, CancellationToken ct) => store.ListAsync(r.CandidateId, r.Page, ct);
    public Task<AnalysisView> Handle(GetCvAnalysisQuery r, CancellationToken ct) => store.GetAsync(r.CandidateId, r.Id, ct);
    public Task<AnalysisView> Handle(ChangeCvAnalysisCommand r, CancellationToken ct) =>
        store.MutateAsync(r.CandidateId, r.Id, r.User, r.Version, r.Action, r.AcknowledgeUnknown, r.Decision, r.Notes, ct, r.FactIndex, r.CorrectedValue);
}
