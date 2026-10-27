using NSubstitute;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Application.HiringProcesses.GetHiringProcessById;
using NexusHR.Hiring.Domain.EligibleCandidates;
using NexusHR.Hiring.Domain.HiringProcesses;

namespace NexusHR.Hiring.UnitTests.HiringProcesses.GetHiringProcessById;

public sealed class GetHiringProcessByIdQueryHandlerTests
{
    private readonly IHiringProcessRepository
        _hiringProcessRepository;

    private readonly IEligibleCandidateRepository
        _eligibleCandidateRepository;

    public GetHiringProcessByIdQueryHandlerTests()
    {
        _hiringProcessRepository =
            Substitute.For<IHiringProcessRepository>();

        _eligibleCandidateRepository =
            Substitute.For<IEligibleCandidateRepository>();
    }

    [Fact]
    public async Task Handle_ShouldReturnDetail_WhenProcessExists()
    {
        var candidateId = Guid.NewGuid();

        var hiringProcess = new HiringProcess(
            Guid.NewGuid(),
            candidateId,
            ".NET Developer",
            "Information Technology",
            EmploymentType.FullTime);

        var candidate = new EligibleCandidate(
            candidateId,
            "Rabbit",
            "Test",
            "rabbit.test.1@nexushr.com",
            DateTime.UtcNow);

        _hiringProcessRepository
            .GetByIdAsync(
                hiringProcess.Id,
                Arg.Any<CancellationToken>())
            .Returns(hiringProcess);

        _eligibleCandidateRepository
            .GetByCandidateIdAsync(
                candidateId,
                Arg.Any<CancellationToken>())
            .Returns(candidate);

        var handler =
            new GetHiringProcessByIdQueryHandler(
                _hiringProcessRepository,
                _eligibleCandidateRepository);

        var response = await handler.Handle(
            new GetHiringProcessByIdQuery(
                hiringProcess.Id),
            CancellationToken.None);

        Assert.NotNull(response);

        Assert.Equal(
            hiringProcess.Id,
            response.Id);

        Assert.Equal(
            candidateId,
            response.CandidateId);

        Assert.Equal(
            "Rabbit Test",
            response.CandidateFullName);

        Assert.Equal(
            "rabbit.test.1@nexushr.com",
            response.CandidateEmail);

        Assert.Equal(
            "Draft",
            response.Status);

        Assert.Equal(
            "FullTime",
            response.EmploymentType);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenProcessDoesNotExist()
    {
        var hiringProcessId = Guid.NewGuid();

        _hiringProcessRepository
            .GetByIdAsync(
                hiringProcessId,
                Arg.Any<CancellationToken>())
            .Returns(
                (HiringProcess?)null);

        var handler =
            new GetHiringProcessByIdQueryHandler(
                _hiringProcessRepository,
                _eligibleCandidateRepository);

        var response = await handler.Handle(
            new GetHiringProcessByIdQuery(
                hiringProcessId),
            CancellationToken.None);

        Assert.Null(response);

        await _eligibleCandidateRepository
            .DidNotReceive()
            .GetByCandidateIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>());
    }
}