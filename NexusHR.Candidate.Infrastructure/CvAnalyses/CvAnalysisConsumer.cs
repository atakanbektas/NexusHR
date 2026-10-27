using MassTransit;
using Microsoft.EntityFrameworkCore;
using NexusHR.Candidate.Domain.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Persistence;
using NexusHR.Contracts.Candidates;

namespace NexusHR.Candidate.Infrastructure.CvAnalyses;

// Short EF Inbox transaction only. The paid network call is deliberately in a separate worker.
internal sealed class CvAnalysisConsumer(CandidateDbContext db) : IConsumer<CvAnalysisRequested>
{
    public async Task Consume(ConsumeContext<CvAnalysisRequested> context)
    {
        var run = await db.Set<CvAnalysis>().SingleOrDefaultAsync(x => x.Id == context.Message.AnalysisId, context.CancellationToken);
        if (run is null || run.Delivered) return;
        run.MarkDelivered(); await db.SaveChangesAsync(context.CancellationToken);
    }
}
