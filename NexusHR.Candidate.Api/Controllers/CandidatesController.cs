using MediatR;
using Microsoft.AspNetCore.Mvc;
using NexusHR.Candidate.Api.Contracts.Candidates;
using NexusHR.Candidate.Application.Candidates.CreateCandidate;
using NexusHR.Candidate.Application.Candidates.GetCandidates;
using NexusHR.Candidate.Application.Candidates.GetCandidateById;


namespace NexusHR.Candidate.Api.Controllers;

[ApiController]
[Route("api/candidates")]
public sealed class CandidatesController(
    ISender sender)
    : ControllerBase
{


    [HttpGet("{id:guid}", Name = nameof(GetCandidateById))]
    public async Task<IActionResult> GetCandidateById(
    Guid id,
    CancellationToken cancellationToken)
    {
        var query = new GetCandidateByIdQuery(id);

        var response = await sender.Send(
            query,
            cancellationToken);

        if (response is null)
        {
            return NotFound(new
            {
                code = "Candidate.NotFound",
                message = "Aday bulunamadı."
            });
        }

        return Ok(response);
    }


    [HttpGet]
    public async Task<IActionResult> GetCandidates(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var query = new GetCandidatesQuery(
            page,
            pageSize);

        var response = await sender.Send(
            query,
            cancellationToken);

        return Ok(response);
    }



    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCandidateRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCandidateCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber);

        var response = await sender.Send(
            command,
            cancellationToken);

        if (!response.IsSuccess)
        {
            var errorResponse = new
            {
                code = response.ErrorCode,
                errors = response.Errors
            };

            if (response.ErrorCode ==
                "Candidate.EmailAlreadyExists")
            {
                return Conflict(errorResponse);
            }

            return BadRequest(errorResponse);
        }

        return CreatedAtRoute(
            nameof(GetCandidateById),
            new
            {
                id = response.CandidateId
            },
            new
            {
                id = response.CandidateId
            });
    }

}