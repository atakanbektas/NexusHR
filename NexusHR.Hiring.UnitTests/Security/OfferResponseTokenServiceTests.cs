using NexusHR.Hiring.Infrastructure.Security;

namespace NexusHR.Hiring.UnitTests.Security;

public sealed class OfferResponseTokenServiceTests
{
    private readonly OfferResponseTokenService _service =
        new();

    [Fact]
    public void Generate_ShouldCreateDifferentTokens()
    {
        var firstToken =
            _service.Generate();

        var secondToken =
            _service.Generate();

        Assert.NotEqual(
            firstToken.PlainText,
            secondToken.PlainText);

        Assert.NotEqual(
            firstToken.Hash,
            secondToken.Hash);
    }

    [Fact]
    public void Generate_ShouldCreateExpectedLengths()
    {
        var token =
            _service.Generate();

        Assert.Equal(
            64,
            token.PlainText.Length);

        Assert.Equal(
            64,
            token.Hash.Length);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_ForValidToken()
    {
        var token =
            _service.Generate();

        var result =
            _service.Verify(
                token.PlainText,
                token.Hash);

        Assert.True(result);
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForInvalidToken()
    {
        var token =
            _service.Generate();

        var result =
            _service.Verify(
                "gecersiz-token",
                token.Hash);

        Assert.False(result);
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForInvalidHashFormat()
    {
        var token =
            _service.Generate();

        var result =
            _service.Verify(
                token.PlainText,
                "gecersiz-hash");

        Assert.False(result);
    }
}