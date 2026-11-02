using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace NexusHR.Onboarding.Tests;

public sealed class IdentityRecoveryTests
{
    [Fact]
    public async Task ColdIdentityDatabaseRestorePreservesRealmClientUserRoleAndAudienceMapper()
    {
        await using var network = new NetworkBuilder().Build(); await network.CreateAsync();
        var dbPassword = Guid.NewGuid().ToString("N"); var userPassword = Guid.NewGuid().ToString("N");
        await using var sourceDb = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("identity_dr").WithUsername("postgres").WithPassword(dbPassword)
            .WithNetwork(network).WithNetworkAliases("source-identity-db").Build();
        await using var targetDb = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("identity_dr").WithUsername("postgres").WithPassword(dbPassword)
            .WithNetwork(network).WithNetworkAliases("target-identity-db").Build();
        await Task.WhenAll(sourceDb.StartAsync(), targetDb.StartAsync());
        var realm = JsonSerializer.SerializeToUtf8Bytes(new {
            realm = "recovery-fixture", enabled = true,
            roles = new { realm = new[] { new { name = "HrSpecialist" } } },
            users = new[] { new { username = "synthetic-user", enabled = true, firstName = "Synthetic", lastName = "Recovery", email = "identity@example.test", emailVerified = true, realmRoles = new[] { "HrSpecialist" },
                credentials = new[] { new { type = "password", value = userPassword, temporary = false } } } },
            clients = new[] { new { clientId = "synthetic-web", enabled = true, publicClient = true, directAccessGrantsEnabled = true,
                protocol = "openid-connect", defaultClientScopes = new[] { "roles", "profile", "email", "basic" }, redirectUris = new[] { "http://localhost:5173/*" }, protocolMappers = new[] {
                    new { name = "test-api-audience", protocol = "openid-connect", protocolMapper = "oidc-audience-mapper",
                        config = new Dictionary<string, string> { ["included.custom.audience"] = "synthetic-api", ["access.token.claim"] = "true" } } } } }
        });
        ContainerBuilder Node(string db) => new ContainerBuilder("quay.io/keycloak/keycloak:26.7.0").WithNetwork(network)
            .WithEnvironment("KC_DB", "postgres").WithEnvironment("KC_DB_URL", $"jdbc:postgresql://{db}:5432/identity_dr")
            .WithEnvironment("KC_DB_USERNAME", "postgres").WithEnvironment("KC_DB_PASSWORD", dbPassword).WithEnvironment("KC_HEALTH_ENABLED", "true")
            .WithPortBinding(8080, true).WithPortBinding(9000, true).WithCommand("start-dev", "--import-realm")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(x => x.ForPort(9000).ForPath("/health/ready"), x => x.WithTimeout(TimeSpan.FromMinutes(3))));
        // Only a disposable synthetic realm enables password grant. No production realm is modified.
        await using var source = Node("source-identity-db").WithResourceMapping(realm, "/opt/keycloak/data/import/recovery-fixture-realm.json").Build();
        await source.StartAsync(); var keyId = await VerifyTokenAsync(source, userPassword); await source.StopAsync();
        var dump = await sourceDb.ExecAsync(["pg_dump", "-U", "postgres", "-d", "identity_dr", "--no-owner", "--no-acl"]); Assert.Equal(0, dump.ExitCode);
        await targetDb.CopyAsync(Encoding.UTF8.GetBytes(dump.Stdout), "/tmp/identity-restore.sql");
        Assert.Equal(0, (await targetDb.ExecAsync(["psql", "-U", "postgres", "-d", "identity_dr", "-v", "ON_ERROR_STOP=on", "-f", "/tmp/identity-restore.sql"])).ExitCode);
        // Deliberately no import file: the restored DB, signing keys and credentials must suffice.
        await using var target = Node("target-identity-db").Build(); await target.StartAsync(); Assert.Equal(keyId, await VerifyTokenAsync(target, userPassword));
    }
    private static async Task<string> VerifyTokenAsync(IContainer server, string password)
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://{server.Hostname}:{server.GetMappedPublicPort(8080)}") };
        using var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "password", ["client_id"] = "synthetic-web", ["username"] = "synthetic-user", ["password"] = password });
        using var response = await client.PostAsync("/realms/recovery-fixture/protocol/openid-connect/token", body); response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); var token = json.GetProperty("access_token").GetString()!;
        var encoded = token.Split('.')[1].Replace('-', '+').Replace('_', '/'); encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
        using var payload = JsonDocument.Parse(Convert.FromBase64String(encoded));
        Assert.Contains(payload.RootElement.GetProperty("realm_access").GetProperty("roles").EnumerateArray(), role => role.GetString() == "HrSpecialist");
        var audience = payload.RootElement.GetProperty("aud");
        Assert.True(audience.ValueKind == JsonValueKind.String ? audience.GetString() == "synthetic-api" : audience.EnumerateArray().Any(x => x.GetString() == "synthetic-api"));
        var header = token.Split('.')[0].Replace('-', '+').Replace('_', '/'); header = header.PadRight((header.Length + 3) / 4 * 4, '=');
        using var metadata = JsonDocument.Parse(Convert.FromBase64String(header)); return metadata.RootElement.GetProperty("kid").GetString()!;
    }
}
