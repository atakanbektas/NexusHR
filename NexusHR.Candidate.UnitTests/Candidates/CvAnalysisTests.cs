using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Domain.CvAnalyses;

namespace NexusHR.Candidate.UnitTests.Candidates;

public sealed class CvAnalysisTests
{
    [Fact]
    public void LeaseAndTerminalStatesPreventDuplicateExecution()
    {
        var now = DateTime.UtcNow;
        var run = new CvAnalysis(Guid.NewGuid(), Guid.NewGuid(), "hr", "key", "test", "test", .1m, now);
        var lease = run.Claim(now, TimeSpan.FromMinutes(1));
        Assert.Throws<InvalidOperationException>(() => run.Claim(now, TimeSpan.FromMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => run.Finish(Guid.NewGuid(), CvAnalysisStatus.Succeeded, "{}", null, now));
        run.Finish(lease, CvAnalysisStatus.OutcomeUnknown, null, "AI.OutcomeUnknown", now);
        Assert.Throws<InvalidOperationException>(() => run.Retry(.1m, now, TimeSpan.Zero, false));
        run.Retry(.1m, now, TimeSpan.Zero, true);
        Assert.Equal(CvAnalysisStatus.Queued, run.Status);
        Assert.Equal(1, run.AttemptCount);
    }
    [Fact]
    public void QueueCancellationStopsClaims()
    {
        var run = new CvAnalysis(Guid.NewGuid(), Guid.NewGuid(), "hr", "key", "test", "test", .1m, DateTime.UtcNow);
        run.RequestCancellation(DateTime.UtcNow);
        Assert.Equal(CvAnalysisStatus.Cancelled, run.Status);
        Assert.Throws<InvalidOperationException>(() => run.Claim(DateTime.UtcNow, TimeSpan.FromMinutes(1)));
    }
    [Theory]
    [InlineData(1, 0, "C#", "C#", true)]
    [InlineData(2, 0, "C#", "C#", false)]
    [InlineData(1, -1, "C#", "C#", false)]
    [InlineData(1, int.MaxValue, "C#", "C#", false)]
    [InlineData(1, 0, "Java", "C#", false)]
    [InlineData(1, 0, "Java", "Java", false)]
    public void EvidenceMustMatchExactPageOffsetAndValue(int page, int start, string value, string quote, bool valid)
    {
        var result = new CvAnalysisResult([new("skill", value, page, start, quote)], []);
        var error = Record.Exception(() => EvidenceValidator.Validate(result, [new(1, "C# engineer")]));
        if (valid) Assert.Null(error); else Assert.IsType<InvalidDataException>(error);
    }
}
