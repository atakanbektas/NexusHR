using NexusHR.Hiring.Domain.HiringProcesses;

namespace NexusHR.Hiring.UnitTests.HiringProcesses;

public sealed class HiringProcessTests
{
    [Fact]
    public void Constructor_ShouldCreateHiringProcess_WithDraftStatus()
    {
        var hiringProcess = CreateHiringProcess();

        Assert.Equal(
            HiringProcessStatus.Draft,
            hiringProcess.Status);

        Assert.Equal(
            "Software Developer",
            hiringProcess.PositionTitle);

        Assert.Equal(
            "Information Technology",
            hiringProcess.Department);

        Assert.Equal(
            EmploymentType.FullTime,
            hiringProcess.EmploymentType);

        Assert.Null(hiringProcess.EmployeeId);
        Assert.NotEqual(default, hiringProcess.CreatedAtUtc);
    }

    [Fact]
    public void PrepareOffer_ShouldStoreOfferDetails_WhenProcessIsDraft()
    {
        var hiringProcess = CreateHiringProcess();

        var proposedStartDate =
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

        var offerExpiresAtUtc =
            DateTime.UtcNow.AddDays(7);

        hiringProcess.PrepareOffer(
            75_000m,
            "try",
            proposedStartDate,
            offerExpiresAtUtc);

        Assert.Equal(
            HiringProcessStatus.OfferPrepared,
            hiringProcess.Status);

        Assert.Equal(
            75_000m,
            hiringProcess.GrossSalary);

        Assert.Equal(
            "TRY",
            hiringProcess.Currency);

        Assert.Equal(
            proposedStartDate,
            hiringProcess.ProposedStartDate);

        Assert.Equal(
            offerExpiresAtUtc,
            hiringProcess.OfferExpiresAtUtc);

        Assert.NotNull(hiringProcess.UpdatedAtUtc);
    }

    [Fact]
    public void PrepareOffer_ShouldThrow_WhenGrossSalaryIsNotPositive()
    {
        var hiringProcess = CreateHiringProcess();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => hiringProcess.PrepareOffer(
                0,
                "TRY",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                DateTime.UtcNow.AddDays(7)));

        Assert.Contains(
            "Brüt maaş sıfırdan büyük olmalıdır.",
            exception.Message);
    }

    [Fact]
    public void SendOffer_ShouldThrow_WhenOfferIsNotPrepared()
    {
        var hiringProcess = CreateHiringProcess();

        var exception = Assert.Throws<InvalidOperationException>(
    () => hiringProcess.SendOffer(
        new string('a', 64)));

        Assert.Equal(
            "Yalnızca hazırlanmış bir teklif gönderilebilir.",
            exception.Message);
    }

    [Fact]
    public void SendOffer_ShouldChangeStatus_WhenOfferIsPrepared()
    {
        var hiringProcess = CreatePreparedHiringProcess();

        hiringProcess.SendOffer(new string('a', 64));

        Assert.Equal(
            HiringProcessStatus.OfferSent,
            hiringProcess.Status);

        Assert.NotNull(hiringProcess.OfferSentAtUtc);
        Assert.NotNull(hiringProcess.UpdatedAtUtc);
    }

    [Fact]
    public void AcceptOffer_ShouldChangeStatus_WhenSentOfferIsActive()
    {
        var hiringProcess = CreateSentHiringProcess();

        var respondedAtUtc = DateTime.UtcNow;

        hiringProcess.AcceptOffer(respondedAtUtc);

        Assert.Equal(
            HiringProcessStatus.OfferAccepted,
            hiringProcess.Status);

        Assert.Equal(
            respondedAtUtc,
            hiringProcess.OfferRespondedAtUtc);

        Assert.Equal(
            respondedAtUtc,
            hiringProcess.UpdatedAtUtc);
    }

    [Fact]
    public void AcceptOffer_ShouldThrow_WhenOfferHasExpired()
    {
        var hiringProcess = CreateSentHiringProcess();

        var respondedAtUtc = DateTime.UtcNow.AddDays(8);

        var exception = Assert.Throws<InvalidOperationException>(
            () => hiringProcess.AcceptOffer(respondedAtUtc));

        Assert.Equal(
            "Süresi dolmuş bir teklif kabul edilemez.",
            exception.Message);

        Assert.Equal(
            HiringProcessStatus.OfferSent,
            hiringProcess.Status);
    }

    [Fact]
    public void RejectOffer_ShouldStoreReason_WhenOfferWasSent()
    {
        var hiringProcess = CreateSentHiringProcess();

        hiringProcess.RejectOffer(
            "Aday başka bir teklifi kabul etti.");

        Assert.Equal(
            HiringProcessStatus.OfferRejected,
            hiringProcess.Status);

        Assert.Equal(
            "Aday başka bir teklifi kabul etti.",
            hiringProcess.RejectionReason);

        Assert.NotNull(hiringProcess.OfferRespondedAtUtc);
    }

    [Fact]
    public void Cancel_ShouldStoreReason_WhenProcessIsActive()
    {
        var hiringProcess = CreateHiringProcess();

        hiringProcess.Cancel(
            "Pozisyon ihtiyacı kaldırıldı.");

        Assert.Equal(
            HiringProcessStatus.Cancelled,
            hiringProcess.Status);

        Assert.Equal(
            "Pozisyon ihtiyacı kaldırıldı.",
            hiringProcess.CancellationReason);

        Assert.NotNull(hiringProcess.UpdatedAtUtc);
    }

    [Fact]
    public void Complete_ShouldSetEmployeeId_WhenOfferWasAccepted()
    {
        var hiringProcess = CreateSentHiringProcess();
        var employeeId = Guid.NewGuid();

        hiringProcess.AcceptOffer(DateTime.UtcNow);
        hiringProcess.Complete(employeeId);

        Assert.Equal(
            HiringProcessStatus.Completed,
            hiringProcess.Status);

        Assert.Equal(
            employeeId,
            hiringProcess.EmployeeId);

        Assert.NotNull(hiringProcess.UpdatedAtUtc);
    }

    [Fact]
    public void Complete_ShouldThrow_WhenOfferWasNotAccepted()
    {
        var hiringProcess = CreateHiringProcess();

        var exception = Assert.Throws<InvalidOperationException>(
            () => hiringProcess.Complete(Guid.NewGuid()));

        Assert.Equal(
            "Yalnızca teklifi kabul edilmiş işe alım süreci tamamlanabilir.",
            exception.Message);

        Assert.Equal(
            HiringProcessStatus.Draft,
            hiringProcess.Status);
    }

    [Fact]
    public void PrepareOffer_ShouldThrow_WhenExpirationIsNotUtc()
    {
        var hiringProcess = CreateHiringProcess();

        var unspecifiedExpiration = new DateTime(
            2027,
            1,
            1,
            12,
            0,
            0,
            DateTimeKind.Unspecified);

        var exception = Assert.Throws<ArgumentException>(
            () => hiringProcess.PrepareOffer(
                75_000m,
                "TRY",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                unspecifiedExpiration));

        Assert.Contains(
            "Teklif son geçerlilik zamanı UTC olmalıdır.",
            exception.Message);
    }

    [Fact]
    public void PrepareOffer_ShouldThrow_WhenOfferWasAlreadySent()
    {
        var hiringProcess = CreateSentHiringProcess();

        var exception = Assert.Throws<InvalidOperationException>(
            () => hiringProcess.PrepareOffer(
                90_000m,
                "TRY",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45)),
                DateTime.UtcNow.AddDays(10)));

        Assert.Equal(
            "Teklif yalnızca taslak veya teklif hazırlanmış durumunda düzenlenebilir.",
            exception.Message);
    }

    private static HiringProcess CreateHiringProcess()
    {
        return new HiringProcess(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Software Developer",
            "Information Technology",
            EmploymentType.FullTime);
    }

    private static HiringProcess CreatePreparedHiringProcess()
    {
        var hiringProcess = CreateHiringProcess();

        hiringProcess.PrepareOffer(
            75_000m,
            "TRY",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            DateTime.UtcNow.AddDays(7));

        return hiringProcess;
    }

    private static HiringProcess CreateSentHiringProcess()
    {
        var hiringProcess = CreatePreparedHiringProcess();

        hiringProcess.SendOffer(new string('a', 64));

        return hiringProcess;
    }
}