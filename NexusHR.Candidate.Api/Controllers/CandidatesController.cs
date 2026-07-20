using MediatR;
using Microsoft.AspNetCore.Mvc;
using NexusHR.Candidate.Api.Contracts.Candidates;
using NexusHR.Candidate.Application.Candidates.CreateCandidate;

namespace NexusHR.Candidate.Api.Controllers;

[ApiController]
[Route("api/candidates")]
public sealed class CandidatesController(
    ISender sender)
    : ControllerBase
{
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

        return Created(
            $"/api/candidates/{response.CandidateId}",
            new
            {
                id = response.CandidateId
            });
    }
}