using NexusHR.BuildingBlocks.Security;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy =
    "FrontendCorsPolicy";

const string AuthenticatedUserPolicy =
    "AuthenticatedUser";

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        FrontendCorsPolicy,
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddKeycloakAuthentication(
    builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthenticatedUserPolicy,
        policy =>
        {
            policy.RequireAuthenticatedUser();
        });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection(
            "ReverseProxy"));

var app = builder.Build();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        status = "Healthy",
        service = "NexusHR.Gateway"
    }));

app.MapReverseProxy();

app.Run();