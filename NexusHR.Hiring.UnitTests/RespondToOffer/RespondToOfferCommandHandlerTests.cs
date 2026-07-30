using NSubstitute;
using NexusHR.Hiring.Application
    .Abstractions.Persistence;
using NexusHR.Hiring.Application
    .Abstractions.Security;
using NexusHR.Hiring.Application
    .HiringProcesses.RespondToOffer;
using NexusHR.Hiring.Domain.HiringProcesses;

namespace NexusHR.Hiring.UnitTests
    .HiringProcesses.RespondToOffer;

public sealed class RespondToOfferCommandHandlerTests
{
    private const string PlainTextToken =
        "candidate-offer-response-token";

    private static readonly string TokenHash =
        new('a', 64);

    private readonly IHiringProcessRepository
        _hiringProcessRepository;

    private readonly IUnitOfWork
        _unitOfWork;

    private readonly IOfferResponseTokenService
        _tokenService;

    private readonly RespondToOfferCommandValidator
        _validator;

    public RespondToOfferCommandHandlerTests()
    {
        _hiringProcessRepository =
            Substitute.For<
                IHiringProcessRepository>();

        _unitOfWork =
            Substitute.For<IUnitOfWork>();

        _tokenService =
            Substitute.For<
                IOfferResponseTokenService>();

        _validator =
            new RespondToOfferCommandValidator();

        _tokenService
            .Hash(PlainTextToken)
            .Returns(TokenHash);
    }

    [Fact]
    public async Task Handle_ShouldAcceptOffer_WhenTokenIsValid()
    {
        var hiringProcess =
            CreateSentHiringProcess();

        _hiringProcessRepository
            .GetByOfferResponseTokenHashAsync(
                TokenHash,
                Arg.Any<CancellationToken>())
            .Returns(hiringProcess);

        _unitOfWork
            .SaveChangesAsync(
                Arg.Any<CancellationToken>())
            .Returns(1);

        var response =
            await CreateHandler().Handle(
                new RespondToOfferCommand(
                    PlainTextToken,
                    OfferDecision.Accept,
                    null),
                CancellationToken.None);

        Assert.True(response.IsSuccess);

        Assert.Equal(
            HiringProcessStatus.OfferAccepted,
            hiringProcess.Status);

        Assert.NotNull(
            hiringProcess.OfferRespondedAtUtc);

        Assert.NotNull(
            hiringProcess
                .OfferResponseTokenUsedAtUtc);

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
    public async Task Handle_ShouldRejectOffer_WhenReasonIsProvided()
    {
        const string rejectionReason =
            "Başka bir iş teklifini kabul ettim.";

        var hiringProcess =
            CreateSentHiringProcess();

        _hiringProcessRepository
            .GetByOfferResponseTokenHashAsync(
                TokenHash,
                Arg.Any<CancellationToken>())
            .Returns(hiringProcess);

        _unitOfWork
            .SaveChangesAsync(
                Arg.Any<CancellationToken>())
            .Returns(1);

        var response =
            await CreateHandler().Handle(
                new RespondToOfferCommand(
                    PlainTextToken,
                    OfferDecision.Reject,
                    rejectionReason),
                CancellationToken.None);

        Assert.True(response.IsSuccess);

        Assert.Equal(
            HiringProcessStatus.OfferRejected,
            hiringProcess.Status);

        Assert.Equal(
            rejectionReason,
            hiringProcess.RejectionReason);

        Assert.NotNull(
            hiringProcess
                .OfferResponseTokenUsedAtUtc);

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
    public async Task Handle_ShouldReturnInvalidToken_WhenProcessCannotBeFound()
    {
        _hiringProcessRepository
            .GetByOfferResponseTokenHashAsync(
                TokenHash,
                Arg.Any<CancellationToken>())
            .Returns(
                (HiringProcess?)null);

        var response =
            await CreateHandler().Handle(
                new RespondToOfferCommand(
                    PlainTextToken,
                    OfferDecision.Accept,
                    null),
                CancellationToken.None);

        Assert.False(response.IsSuccess);

        Assert.Equal(
            "OfferResponse.InvalidToken",
            response.ErrorCode);

        await _hiringProcessRepository
            .DidNotReceive()
            .UpdateAsync(
                Arg.Any<HiringProcess>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenRejectionReasonIsEmpty()
    {
        var response =
            await CreateHandler().Handle(
                new RespondToOfferCommand(
                    PlainTextToken,
                    OfferDecision.Reject,
                    null),
                CancellationToken.None);

        Assert.False(response.IsSuccess);

        Assert.Equal(
            "OfferResponse.ValidationFailed",
            response.ErrorCode);

        _tokenService
            .DidNotReceive()
            .Hash(
                Arg.Any<string>());

        await _hiringProcessRepository
            .DidNotReceive()
            .GetByOfferResponseTokenHashAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    private RespondToOfferCommandHandler
        CreateHandler()
    {
        return new RespondToOfferCommandHandler(
            _hiringProcessRepository,
            _unitOfWork,
            _tokenService,
            _validator);
    }

    private static HiringProcess
        CreateSentHiringProcess()
    {
        var hiringProcess =
            new HiringProcess(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ".NET Developer",
                "Information Technology",
                EmploymentType.FullTime);

        hiringProcess.PrepareOffer(
            95_000m,
            "TRY",
            DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(30)),
            DateTime.UtcNow.AddDays(7));

        hiringProcess.SendOffer(
            TokenHash);

        return hiringProcess;
    }
}