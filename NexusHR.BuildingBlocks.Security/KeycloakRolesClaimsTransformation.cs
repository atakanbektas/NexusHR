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

        using var realmAccess =
            JsonDocument.Parse(realmAccessClaim.Value);

        if (!realmAccess.RootElement.TryGetProperty(
                "roles",
                out var rolesElement))
        {
            return Task.FromResult(principal);
        }

        if (rolesElement.ValueKind !=
            JsonValueKind.Array)
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