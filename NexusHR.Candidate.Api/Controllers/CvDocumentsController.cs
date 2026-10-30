using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusHR.Candidate.Application.CvAnalyses;

namespace NexusHR.Candidate.Api.Controllers;

[ApiController]
[Authorize(Roles = "HrSpecialist,HrManager,Auditor")]
[Route("api/candidates/{candidateId:guid}/documents/cv/versions")]
[ServiceFilter(typeof(CvAnalysisExceptionFilter))]
public sealed class CvDocumentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid candidateId, CancellationToken ct) => Ok(await sender.Send(new ListCvDocumentVersions(candidateId), ct));

    [HttpGet("{documentId:guid}")]
    public async Task<IActionResult> Download(Guid candidateId, Guid documentId, CancellationToken ct)
    {
        var doc = await sender.Send(new DownloadCvDocumentVersion(candidateId, documentId), ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(doc.Content, doc.ContentType, doc.FileName);
    }
}
