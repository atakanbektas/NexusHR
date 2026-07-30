using NSubstitute;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Application.HiringProcesses
    .GetActiveHiringProcessByCandidateId;
using NexusHR.Hiring.Domain.HiringProcesses;

namespace NexusHR.Hiring.UnitTests.HiringProcesses
    .GetActiveHiringProcessByCandidateId;

public sealed class GetActiveHiringProcessByCandidateIdQueryHandlerTests
{
    private readonly IHiringProcessRepository
        _hiringProcessRepository;

    public GetActiveHiringProcessByCandidateIdQueryHandlerTests()
    {
        _hiringProcessRepository =
            Substitute.For<IHiringProcessRepository>();
    }

    [Fact]
    public async Task Handle_ShouldReturnActiveProcess_WhenItExists()
    {
        var hiringProcess = CreateDraftHiringProcess();

        _hiringProcessRepository
            .GetActiveByCandidateIdAsync(
                hiringProcess.CandidateId,
                Arg.Any<CancellationToken>())
            .Returns(hiringProcess);

        var response = await CreateHandler().Handle(
            new GetActiveHiringProcessByCandidateIdQuery(
                hiringProcess.CandidateId),
            CancellationToken.None);

        Assert.True(response.Exists);
        Assert.Equal(hiringProcess.Id, response.HiringProcessId);
        Assert.Equal(
            HiringProcessStatus.Draft.ToString(),
            response.Status);

        await _hiringProcessRepository
            .DidNotReceive()
            .GetLatestByCandidateIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnLatestInactiveStatus_WhenNoActiveProcessExists()
    {
        var hiringProcess = CreateRejectedHiringProcess();

        _hiringProcessRepository
            .GetActiveByCandidateIdAsync(
                hiringProcess.CandidateId,
                Arg.Any<CancellationToken>())
            .Returns((HiringProcess?)null);

        _hiringProcessRepository
            .GetLatestByCandidateIdAsync(
                hiringProcess.CandidateId,
                Arg.Any<CancellationToken>())
            .Returns(hiringProcess);

        var response = await CreateHandler().Handle(
            new GetActiveHiringProcessByCandidateIdQuery(
                hiringProcess.CandidateId),
            CancellationToken.None);

        Assert.False(response.Exists);
        Assert.Null(response.HiringProcessId);
        Assert.Equal(
            HiringProcessStatus.OfferRejected.ToString(),
            response.Status);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCandidateHasNoProcess()
    {
        var candidateId = Guid.NewGuid();

        _hiringProcessRepository
            .GetActiveByCandidateIdAsync(
                candidateId,
                Arg.Any<CancellationToken>())
            .Returns((HiringProcess?)null);

        _hiringProcessRepository
            .GetLatestByCandidateIdAsync(
                candidateId,
                Arg.Any<CancellationToken>())
            .Returns((HiringProcess?)null);

        var response = await CreateHandler().Handle(
            new GetActiveHiringProcessByCandidateIdQuery(
                candidateId),
            CancellationToken.None);

        Assert.False(response.Exists);
        Assert.Null(response.HiringProcessId);
        Assert.Null(response.Status);
    }

    private GetActiveHiringProcessByCandidateIdQueryHandler
        CreateHandler()
    {
        return new GetActiveHiringProcessByCandidateIdQueryHandler(
            _hiringProcessRepository);
    }

    private static HiringProcess CreateDraftHiringProcess()
    {
        return new HiringProcess(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ".NET Developer",
            "Information Technology",
            EmploymentType.FullTime);
    }

    private static HiringProcess CreateRejectedHiringProcess()
    {
        var hiringProcess = CreateDraftHiringProcess();

        hiringProcess.PrepareOffer(
            95_000m,
            "TRY",
            DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(30)),
            DateTime.UtcNow.AddDays(7));

        hiringProcess.SendOffer(
            new string('a', 64));

        hiringProcess.RejectOffer(
            "Başka bir teklifi kabul ettim.");

        return hiringProcess;
    }
}
