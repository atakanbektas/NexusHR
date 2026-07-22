var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy =
    "FrontendCorsPolicy";

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

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection(
            "ReverseProxy"));

var app = builder.Build();

app.UseCors(FrontendCorsPolicy);

app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        status = "Healthy",
        service = "NexusHR.Gateway"
    }));

app.MapReverseProxy();

app.Run();