using NexusHR.Candidate.Domain.Candidates;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusHR.Candidate.UnitTests.Candidates
{
    public class CandidateTests
    {
        [Fact]
        public void Constructor_ShouldCreateCandidate_WithDraftStatus()
        {
            var candidate = new Domain.Candidates.Candidate(
                Guid.NewGuid(),
                "Atakan",
                "Bektaş",
                "atakan@example.com",
                "05555555555");

            Assert.Equal(CandidateStatus.Draft, candidate.Status);
            Assert.Equal("atakan@example.com", candidate.Email);
        }

        [Fact]
        public void MarkReadyForHiring_ShouldThrow_WhenDocumentsAreNotPending()
        {
            var candidate = new Domain.Candidates.Candidate(
                Guid.NewGuid(),
                "Atakan",
                "Bektaş",
                "atakan@example.com",
                "05555555555");

            Assert.Throws<InvalidOperationException>(
                candidate.MarkReadyForHiring);
        }

        [Fact]
        public void Candidate_ShouldBecomeReadyForHiring_WhenDocumentsArePending()
        {
            var candidate = new Domain.Candidates.Candidate(
                Guid.NewGuid(),
                "Atakan",
                "Bektaş",
                "atakan@example.com",
                "05555555555");

            candidate.MarkDocumentsPending();
            candidate.MarkReadyForHiring();

            Assert.Equal(
                CandidateStatus.ReadyForHiring,
                candidate.Status);
        }
    }
}
