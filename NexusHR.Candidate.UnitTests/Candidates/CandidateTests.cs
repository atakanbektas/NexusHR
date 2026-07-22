using NexusHR.Candidate.Domain.Candidates;

namespace NexusHR.Candidate.UnitTests.Candidates;

public sealed class CandidateTests
{
    [Fact]
    public void Constructor_ShouldCreateCandidate_WithDraftStatus()
    {
        var candidate = CreateCandidate();

        Assert.Equal(
            CandidateStatus.Draft,
            candidate.Status);

        Assert.Equal(
            "atakan@example.com",
            candidate.Email);
    }

    [Fact]
    public void MarkDocumentsPending_ShouldChangeStatus_WhenCandidateIsDraft()
    {
        var candidate = CreateCandidate();

        candidate.MarkDocumentsPending();

        Assert.Equal(
            CandidateStatus.DocumentsPending,
            candidate.Status);

        Assert.NotNull(candidate.UpdatedAtUtc);
    }

    [Fact]
    public void MarkDocumentsPending_ShouldThrow_WhenCandidateIsNotDraft()
    {
        var candidate = CreateCandidate();

        candidate.MarkDocumentsPending();

        var exception = Assert.Throws<InvalidOperationException>(
            candidate.MarkDocumentsPending);

        Assert.Equal(
            "Yalnızca taslak aday belge bekliyor durumuna alınabilir.",
            exception.Message);
    }

    [Fact]
    public void MarkReadyForHiring_ShouldThrow_WhenCandidateIsDraft()
    {
        var candidate = CreateCandidate();

        var exception = Assert.Throws<InvalidOperationException>(
            candidate.MarkReadyForHiring);

        Assert.Equal(
            "Aday belge bekliyor durumunda olmalıdır.",
            exception.Message);
    }

    [Fact]
    public void MarkReadyForHiring_ShouldChangeStatus_WhenDocumentsArePending()
    {
        var candidate = CreateCandidate();

        candidate.MarkDocumentsPending();
        candidate.MarkReadyForHiring();

        Assert.Equal(
            CandidateStatus.ReadyForHiring,
            candidate.Status);

        Assert.NotNull(candidate.UpdatedAtUtc);
    }

    [Fact]
    public void MarkReadyForHiring_ShouldThrow_WhenCandidateIsAlreadyReady()
    {
        var candidate = CreateCandidate();

        candidate.MarkDocumentsPending();
        candidate.MarkReadyForHiring();

        var exception = Assert.Throws<InvalidOperationException>(
            candidate.MarkReadyForHiring);

        Assert.Equal(
            "Aday belge bekliyor durumunda olmalıdır.",
            exception.Message);
    }

    [Fact]
    public void UpdateInformation_ShouldThrow_WhenCandidateIsArchived()
    {
        var candidate = CreateCandidate();

        candidate.Archive();

        var exception = Assert.Throws<InvalidOperationException>(
            () => candidate.UpdateInformation(
                "Mehmet",
                "Yılmaz",
                "mehmet@example.com",
                "05551112233"));

        Assert.Equal(
            "Arşivlenmiş aday güncellenemez.",
            exception.Message);
    }

    [Fact]
    public void Archive_ShouldChangeStatus_ToArchived()
    {
        var candidate = CreateCandidate();

        candidate.Archive();

        Assert.Equal(
            CandidateStatus.Archived,
            candidate.Status);

        Assert.NotNull(candidate.UpdatedAtUtc);
    }

    private static Domain.Candidates.Candidate CreateCandidate()
    {
        return new Domain.Candidates.Candidate(
            Guid.NewGuid(),
            "Atakan",
            "Bektaş",
            "atakan@example.com",
            "05555555555");
    }
}