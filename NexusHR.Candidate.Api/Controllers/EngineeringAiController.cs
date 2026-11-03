using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusHR.Candidate.Application.CvAnalyses;
namespace NexusHR.Candidate.Api.Controllers;

[ApiController, Route("api/engineering-ai"), Authorize(Roles="EngineeringReader,EngineeringOperator")]
public sealed class EngineeringAiController(ISender sender) : ControllerBase
{
    [HttpGet] public Task<AiDiagnostics> Get(CancellationToken ct) => sender.Send(new GetEngineeringAiDiagnostics(), ct);
}
