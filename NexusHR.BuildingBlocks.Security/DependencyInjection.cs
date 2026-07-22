using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace NexusHR.BuildingBlocks.Security;

public static class DependencyInjection
{
    public static IServiceCollection
        AddKeycloakAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
    {
        var authority =
            configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException(
                "Keycloak Authority ayarı bulunamadı.");

        var metadataAddress =
            configuration["Keycloak:MetadataAddress"]
            ?? throw new InvalidOperationException(
                "Keycloak MetadataAddress ayarı bulunamadı.");

        var clientId =
            configuration["Keycloak:ClientId"]
            ?? throw new InvalidOperationException(
                "Keycloak ClientId ayarı bulunamadı.");

        var requireHttpsMetadata =
            bool.TryParse(
                configuration[
                    "Keycloak:RequireHttpsMetadata"],
                out var parsedValue)
            && parsedValue;

        services
            .AddAuthentication(
                JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.MetadataAddress =
                    metadataAddress;

                options.RequireHttpsMetadata =
                    requireHttpsMetadata;

                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = authority,

                        ValidateAudience = false,

                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        NameClaimType =
                            "preferred_username",

                        RoleClaimType =
                            ClaimTypes.Role,

                        ClockSkew =
                            TimeSpan.FromSeconds(30)
                    };

                options.Events =
                    new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var authorizedParty =
                                context.Principal?
                                    .FindFirst("azp")
                                    ?.Value;

                            if (!string.Equals(
                                    authorizedParty,
                                    clientId,
                                    StringComparison.Ordinal))
                            {
                                context.Fail(
                                    "Token farklı bir istemci için oluşturulmuş.");
                            }

                            return Task.CompletedTask;
                        }
                    };
            });

        services.AddTransient<
            IClaimsTransformation,
            KeycloakRolesClaimsTransformation>();

        services.AddAuthorization();

        return services;
    }
}