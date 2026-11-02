using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace NexusHR.BuildingBlocks.Security;

internal sealed class KeycloakRolesClaimsTransformation
    : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(
        ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return Task.FromResult(principal);
        }

        var realmAccessClaim =
            identity.FindFirst("realm_access");

        if (realmAccessClaim is null)
        {
            return Task.FromResult(principal);
        }

        if (realmAccessClaim.Value.Length > 16384) return Task.FromResult(principal);
        JsonDocument realmAccess;
        try { realmAccess = JsonDocument.Parse(realmAccessClaim.Value, new() { MaxDepth = 8 }); }
        catch (JsonException) { return Task.FromResult(principal); }
        using var document = realmAccess;

        if (realmAccess.RootElement.ValueKind != JsonValueKind.Object || !realmAccess.RootElement.TryGetProperty(
                "roles",
                out var rolesElement))
        {
            return Task.FromResult(principal);
        }

        if (rolesElement.ValueKind !=
            JsonValueKind.Array || rolesElement.GetArrayLength() > 100 ||
            rolesElement.EnumerateArray().Any(role => role.ValueKind != JsonValueKind.String || role.GetString()!.Length > 128))
        {
            return Task.FromResult(principal);
        }

        foreach (var roleElement in
                 rolesElement.EnumerateArray())
        {
            var role = roleElement.GetString();

            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }

            if (identity.HasClaim(
                    ClaimTypes.Role,
                    role))
            {
                continue;
            }

            identity.AddClaim(
                new Claim(
                    ClaimTypes.Role,
                    role));
        }

        return Task.FromResult(principal);
    }
}
