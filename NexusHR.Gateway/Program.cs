using NexusHR.BuildingBlocks.Security;
using NexusHR.Gateway.CandidateOverview;

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

builder.Services.AddHttpClient(
    CandidateOverviewHttpClients.CandidateApi,
    client =>
    {
        client.BaseAddress = GetClusterAddress(
            builder.Configuration,
            "candidate-cluster",
            "candidate-api");
    });

builder.Services.AddHttpClient(
    CandidateOverviewHttpClients.HiringApi,
    client =>
    {
        client.BaseAddress = GetClusterAddress(
            builder.Configuration,
            "hiring-cluster",
            "hiring-api");
    });

builder.Services.AddScoped<CandidateOverviewService>();

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

app.MapCandidateOverviewEndpoints(
    AuthenticatedUserPolicy);

app.MapReverseProxy();

app.Run();

static Uri GetClusterAddress(
    IConfiguration configuration,
    string clusterId,
    string destinationId)
{
    var address = configuration[
        $"ReverseProxy:Clusters:{clusterId}:Destinations:{destinationId}:Address"];

    if (string.IsNullOrWhiteSpace(address) ||
        !Uri.TryCreate(
            address,
            UriKind.Absolute,
            out var uri))
    {
        throw new InvalidOperationException(
            $"{clusterId}/{destinationId} adresi yapılandırılmamış.");
    }

    return uri;
}
