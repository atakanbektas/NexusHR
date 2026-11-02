using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using NexusHR.BuildingBlocks.Hosting;

namespace NexusHR.Platform.Tests;

public sealed class StagingConfigurationTests
{
    [Theory]
    [InlineData("Keycloak:Authority", "http://identity.example.test/realm")]
    [InlineData("Keycloak:MetadataAddress", "http://identity.example.test/discovery")]
    [InlineData("Keycloak:Audience", "")]
    [InlineData("Keycloak:RequireHttpsMetadata", "false")]
    [InlineData("RabbitMq:UseSsl", "false")]
    public void StagingRejectsInsecureIdentityOrBrokerConfiguration(string key, string value)
    {
        var builder = Builder(); builder.Configuration[key] = value;
        Assert.Throws<InvalidOperationException>(() => builder.AddApiDefaults());
    }
    [Theory]
    [InlineData("http://app.example.test")]
    [InlineData("https://app.example.test/private")]
    [InlineData("*")]
    public void StagingRejectsInsecureOrWildcardCors(string origin)
    {
        var builder = Builder(); builder.Configuration["Cors:AllowedOrigins:0"] = origin;
        Assert.Throws<InvalidOperationException>(() => builder.GetFrontendOrigins());
    }
    [Fact]
    public void ExplicitSecureSettingsAndCanonicalOriginsAreAccepted()
    {
        var builder = Builder(); builder.Configuration["Cors:AllowedOrigins:0"] = "https://app.example.test/";
        builder.AddApiDefaults(); Assert.Equal(["https://app.example.test"], builder.GetFrontendOrigins());
    }
    private static WebApplicationBuilder Builder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Keycloak:Authority"] = "https://identity.example.test/realm", ["Keycloak:MetadataAddress"] = "https://identity.example.test/discovery",
            ["Keycloak:Audience"] = "synthetic-api", ["Keycloak:RequireHttpsMetadata"] = "true", ["RabbitMq:Host"] = "mq.example.test", ["RabbitMq:UseSsl"] = "true" });
        return builder;
    }
}
