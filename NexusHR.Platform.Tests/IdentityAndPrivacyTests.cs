using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusHR.BuildingBlocks.Security;

namespace NexusHR.Platform.Tests;

public sealed class IdentityAndPrivacyTests
{
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"roles\":[\"Admin\",12]}")]
    [InlineData("{\"roles\":[null]}")]
    [InlineData("{\"roles\":{\"Admin\":true}}")]
    public async Task MalformedRealmRolesNeverCrashOrPartiallyGrantPrivileges(string json)
    {
        using var services = Services();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("realm_access", json)], "fixture"));
        var result = await services.GetRequiredService<IClaimsTransformation>().TransformAsync(principal);
        Assert.Empty(result.FindAll(ClaimTypes.Role)); Assert.False(result.IsInRole("Admin"));
    }
    [Fact]
    public async Task ValidRoleMappingRemainsIdempotent()
    {
        using var services = Services(); var transform = services.GetRequiredService<IClaimsTransformation>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("realm_access", "{\"roles\":[\"HrSpecialist\",\"Auditor\",\"HrSpecialist\"]}")], "fixture"));
        await transform.TransformAsync(principal); await transform.TransformAsync(principal);
        Assert.Equal(2, principal.FindAll(ClaimTypes.Role).Count()); Assert.True(principal.IsInRole("HrSpecialist"));
    }
    private static ServiceProvider Services() => new ServiceCollection().AddLogging().AddKeycloakAuthentication(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Keycloak:Authority"] = "https://identity.example.test/realms/test", ["Keycloak:MetadataAddress"] = "https://identity.example.test/discovery",
            ["Keycloak:ClientId"] = "synthetic-client"
        }).Build()).BuildServiceProvider();
}
