using NSubstitute;
using NexusHR.Contracts.Hiring;
using NexusHR.Hiring.Application.Abstractions.Messaging;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Application.Abstractions.Security;
using NexusHR.Hiring.Application.HiringProcesses.SendOffer;
using NexusHR.Hiring.Domain.EligibleCandidates;
using NexusHR.Hiring.Domain.HiringProcesses;

namespace NexusHR.Hiring.UnitTests.HiringProcesses.SendOffer;

public sealed class SendOfferCommandHandlerTests
{
    private const string PlainTextToken =
        "candidate-offer-response-token";

    private static readonly string TokenHash =
        new('a', 64);

    private readonly IHiringProcessRepository
        _hiringProcessRepository;

    private readonly IEligibleCandidateRepository
        _eligibleCandidateRepository;

    private readonly IUnitOfWork
        _unitOfWork;

    private readonly IOfferResponseTokenService
        _tokenService;

    private readonly IIntegrationEventPublisher
        _integrationEventPublisher;

    private readonly SendOfferCommandValidator
        _validator;

    public SendOfferCommandHandlerTests()
    {
        _hiringProcessRepository =
            Substitute.For<
                IHiringProcessRepository>();

        _eligibleCandidateRepository =
            Substitute.For<
                IEligibleCandidateRepository>();

        _unitOfWork =
            Substitute.For<IUnitOfWork>();

        _tokenService =
            Substitute.For<
                IOfferResponseTokenService>();

        _integrationEventPublisher =
            Substitute.For<
                IIntegrationEventPublisher>();

        _validator =
            new SendOfferCommandValidator();

        _tokenService.Generate()
            .Returns(
                new GeneratedOfferResponseToken(
                    PlainTextToken,
                    TokenHash));
    }

    [Fact]
    public async Task Handle_ShouldSendOfferAndPublishEvent_WhenOfferIsPrepared()
    {
        var hiringProcess =
            CreatePreparedHiringProcess();

        var eligibleCandidate =
            CreateEligibleCandidate(
                hiringProcess.CandidateId);

        _hiringProcessRepository
            .GetByIdAsync(
                hiringProcess.Id,
                Arg.Any<CancellationToken>())
            .Returns(hiringProcess);

        _eligibleCandidateRepository
            .GetByCandidateIdAsync(
                hiringProcess.CandidateId,
                Arg.Any<CancellationToken>())
            .Returns(eligibleCandidate);

        _unitOfWork
            .SaveChangesAsync(
                Arg.Any<CancellationToken>())
            .Returns(1);

        var handler = CreateHandler();

        var response = await handler.Handle(
            new SendOfferCommand(
                hiringProcess.Id),
            CancellationToken.None);

        Assert.True(response.IsSuccess);

        Assert.Equal(
            HiringProcessStatus.OfferSent,
            hiringProcess.Status);

        Assert.Equal(
            TokenHash,
            hiringProcess
                .OfferResponseTokenHash);

        Assert.NotNull(
            hiringProcess.OfferSentAtUtc);


        await _integrationEventPublisher
            .Received(1)
            .PublishAsync(
                Arg.Is<
                    HiringOfferSentIntegrationEvent>(
                    integrationEvent =>
                        integrationEvent
                            .HiringProcessId ==
                        hiringProcess.Id &&
                        integrationEvent
                            .CandidateEmail ==
                        eligibleCandidate.Email &&
                        integrationEvent
                            .ResponseToken ==
                        PlainTextToken),
                Arg.Any<CancellationToken>());

        await _hiringProcessRepository
            .Received(1)
            .UpdateAsync(
                hiringProcess,
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenProcessDoesNotExist()
    {
        var hiringProcessId =
            Guid.NewGuid();

        _hiringProcessRepository
            .GetByIdAsync(
                hiringProcessId,
                Arg.Any<CancellationToken>())
            .Returns(
                (HiringProcess?)null);

        var response =
            await CreateHandler().Handle(
                new SendOfferCommand(
                    hiringProcessId),
                CancellationToken.None);

        Assert.False(response.IsSuccess);

        Assert.Equal(
            "HiringProcess.NotFound",
            response.ErrorCode);

        await _integrationEventPublisher
            .DidNotReceive()
            .PublishAsync(
                Arg.Any<
                    HiringOfferSentIntegrationEvent>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnInvalidStatus_WhenProcessIsDraft()
    {
        var hiringProcess =
            CreateDraftHiringProcess();

        var eligibleCandidate =
            CreateEligibleCandidate(
                hiringProcess.CandidateId);

        _hiringProcessRepository
            .GetByIdAsync(
                hiringProcess.Id,
                Arg.Any<CancellationToken>())
            .Returns(hiringProcess);

        _eligibleCandidateRepository
            .GetByCandidateIdAsync(
                hiringProcess.CandidateId,
                Arg.Any<CancellationToken>())
            .Returns(eligibleCandidate);

        var response =
            await CreateHandler().Handle(
                new SendOfferCommand(
                    hiringProcess.Id),
                CancellationToken.None);

        Assert.False(response.IsSuccess);

        Assert.Equal(
            "HiringProcess.InvalidStatus",
            response.ErrorCode);

        await _integrationEventPublisher
            .DidNotReceive()
            .PublishAsync(
                Arg.Any<
                    HiringOfferSentIntegrationEvent>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenIdIsEmpty()
    {
        var response =
            await CreateHandler().Handle(
                new SendOfferCommand(
                    Guid.Empty),
                CancellationToken.None);

        Assert.False(response.IsSuccess);

        Assert.Equal(
            "HiringProcess.ValidationFailed",
            response.ErrorCode);

        await _hiringProcessRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>());
    }

    private SendOfferCommandHandler CreateHandler()
    {
        return new SendOfferCommandHandler(
            _hiringProcessRepository,
            _eligibleCandidateRepository,
            _unitOfWork,
            _tokenService,
            _integrationEventPublisher,
            _validator);
    }

    private static EligibleCandidate
        CreateEligibleCandidate(
            Guid candidateId)
    {
        return new EligibleCandidate(
            candidateId,
            "Rabbit",
            "Test",
            "rabbit.test@nexushr.com",
            DateTime.UtcNow.AddDays(-1));
    }

    private static HiringProcess
        CreateDraftHiringProcess()
    {
        return new HiringProcess(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ".NET Developer",
            "Information Technology",
            EmploymentType.FullTime);
    }

    private static HiringProcess
        CreatePreparedHiringProcess()
    {
        var hiringProcess =
            CreateDraftHiringProcess();

        hiringProcess.PrepareOffer(
            95_000m,
            "TRY",
            DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(30)),
            DateTime.UtcNow.AddDays(7));

        return hiringProcess;
    }
}