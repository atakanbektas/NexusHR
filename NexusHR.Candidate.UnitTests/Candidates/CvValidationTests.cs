using NexusHR.Candidate.Application.Candidates.UploadCandidateCv;
using NexusHR.Candidate.Application.Candidates.ExtractCandidateFromCv;

namespace NexusHR.Candidate.UnitTests.Candidates;

public sealed class CvValidationTests
{
    [Fact]
    public async Task NullFileInputsReturnValidationFailuresInsteadOfThrowing()
    {
        var upload = await new UploadCandidateCvCommandValidator().ValidateAsync(
            new UploadCandidateCvCommand(Guid.NewGuid(), null!, "application/pdf", null!));
        Assert.False(upload.IsValid);
        var extraction = await new ExtractCandidateFromCvCommandValidator().ValidateAsync(
            new ExtractCandidateFromCvCommand(null!, null!));
        Assert.False(extraction.IsValid);
    }

    [Fact]
    public async Task SpoofedAndOversizedFilesAreRejected()
    {
        var validator = new UploadCandidateCvCommandValidator();
        Assert.False((await validator.ValidateAsync(new UploadCandidateCvCommand(Guid.NewGuid(), "cv.pdf", "application/pdf", [1, 2, 3]))).IsValid);
        var large = new byte[5 * 1024 * 1024 + 1];
        "%PDF-"u8.CopyTo(large);
        Assert.False((await validator.ValidateAsync(new UploadCandidateCvCommand(Guid.NewGuid(), "cv.pdf", "application/pdf", large))).IsValid);
    }
}
