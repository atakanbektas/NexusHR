using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexusHR.BuildingBlocks.Security;
using NexusHR.Candidate.Application.CvAnalyses;

namespace NexusHR.Candidate.Api.Controllers;

[ApiController]
[Authorize(Roles = "HrSpecialist,HrManager,Auditor")]
[Route("api/candidates/{candidateId:guid}/cv-analyses")]
[ServiceFilter(typeof(CvAnalysisExceptionFilter))]
public sealed class CvAnalysesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid candidateId, [FromQuery] int page = 1, CancellationToken ct = default) =>
        Ok(await sender.Send(new ListCvAnalysesQuery(candidateId, page), ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid candidateId, Guid id, CancellationToken ct) => Ok(await sender.Send(new GetCvAnalysisQuery(candidateId, id), ct));
    [HttpPost]
    [Authorize(Roles = NexusHrRoles.CandidateEditors)]
    public async Task<IActionResult> Create(Guid candidateId, RequestAnalysisBody body, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var result = await sender.Send(new RequestCvAnalysisCommand(candidateId, body.DocumentId, Subject(), key, body.AcknowledgeExternalProcessing), ct);
        return AcceptedAtAction(nameof(Get), new { candidateId, id = result.Id }, result);
    }
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = NexusHrRoles.CandidateEditors)]
    public async Task<IActionResult> Cancel(Guid candidateId, Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new ChangeCvAnalysisCommand(candidateId, id, Subject(), Version(), "cancel"), ct));
    [HttpPost("{id:guid}/retry")]
    [Authorize(Roles = NexusHrRoles.CandidateEditors)]
    public async Task<IActionResult> Retry(Guid candidateId, Guid id, RetryAnalysisBody body, CancellationToken ct) =>
        Ok(await sender.Send(new ChangeCvAnalysisCommand(candidateId, id, Subject(), Version(), "retry", body.AcknowledgePossibleDuplicateCharge), ct));
    [HttpPut("{id:guid}/review")]
    [Authorize(Roles = NexusHrRoles.CandidateEditors)]
    public async Task<IActionResult> Review(Guid candidateId, Guid id, ReviewAnalysisBody body, CancellationToken ct) =>
        Ok(await sender.Send(new ChangeCvAnalysisCommand(candidateId, id, Subject(), Version(), "review", Decision: body.Decision, Notes: body.Notes), ct));
    private string Subject() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "";
    private uint Version() => uint.TryParse(Request.Headers.IfMatch.ToString().Trim('"'), out var version) ? version :
        throw new CvAnalysisException(428, "AI.IfMatchRequired");
}
public sealed record RequestAnalysisBody(Guid DocumentId, bool AcknowledgeExternalProcessing = false);
public sealed record RetryAnalysisBody(bool AcknowledgePossibleDuplicateCharge = false);
public sealed record ReviewAnalysisBody(string Decision, string? Notes);
public sealed class CvAnalysisExceptionFilter : IExceptionFilter, IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: AnalysisView view })
            context.HttpContext.Response.Headers.ETag = $"\"{view.Version}\"";
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not CvAnalysisException error) return;
        var problem = new ProblemDetails { Status = error.Status, Title = error.Code };
        problem.Extensions["code"] = error.Code; problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        context.Result = new ObjectResult(problem) { StatusCode = error.Status }; context.ExceptionHandled = true;
    }
}
