using NexusHR.Notification.Infrastructure;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddInfrastructure(
    builder.Configuration);

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();