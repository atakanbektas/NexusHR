using NexusHR.BuildingBlocks.Hosting;
using NexusHR.Notification.Infrastructure;

var builder =
    WebApplication.CreateBuilder(args);



builder.Services.AddInfrastructure(
    builder.Configuration);

builder.AddApiDefaults();
var app = builder.Build();
app.UseApiDefaults();

app.MapHealthChecks("/health");

app.Run();
