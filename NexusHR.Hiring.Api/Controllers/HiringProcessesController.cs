using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusHR.BuildingBlocks.Security;
using NexusHR.Hiring.Api.Contracts.HiringProcesses;
using NexusHR.Hiring.Application.HiringProcesses.CreateHiringProcess;
using NexusHR.Hiring.Application.HiringProcesses.GetActiveHiringProcessByCandidateId;
using NexusHR.Hiring.Application.HiringProcesses.GetHiringProcessById;
using NexusHR.Hiring.Application.HiringProcesses.PrepareOffer;
using NexusHR.Hiring.Application.HiringProcesses.SendOffer;
using NexusHR.Hiring.Application.HiringProcesses.GetActiveHiringProcessByCandidateIds;

namespace NexusHR.Hiring.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/hiring-processes")]
public sealed class HiringProcessesController(
    ISender sender)
    : ControllerBase
{

    [HttpGet("active/by-candidate/{candidateId:guid}")]
    [Authorize(Roles = NexusHrRoles.HiringReaders)]
    public async Task<IActionResult> GetActiveByCandidateId(
    Guid candidateId,
    CancellationToken cancellationToken)
    {
        var query =
            new GetActiveHiringProcessByCandidateIdQuery(
                candidateId);

        var response = await sender.Send(
            query,
            cancellationToken);

        return Ok(response);
    }


    [HttpPost]
    [Authorize(Roles = NexusHrRoles.HiringEditors)]
    public async Task<IActionResult> Create(
        CreateHiringProcessRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateHiringProcessCommand(
            request.CandidateId,
            request.PositionTitle,
            request.Department,
            request.EmploymentType);

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

            return response.ErrorCode switch
            {
                "HiringProcess.CandidateNotEligible" =>
                    Conflict(errorResponse),

                "HiringProcess.ActiveProcessAlreadyExists" =>
                    Conflict(errorResponse),

                _ => BadRequest(errorResponse)
            };
        }

        return CreatedAtRoute(
            nameof(GetById),
            new
            {
                id = response.HiringProcessId
            },
            new
            {
                id = response.HiringProcessId
            });
    }

    [HttpPut("{id:guid}/offer")]
    [Authorize(Roles = NexusHrRoles.HiringEditors)]
    public async Task<IActionResult> PrepareOffer(
    Guid id,
    PrepareOfferRequest request,
    CancellationToken cancellationToken)
    {
        var command = new PrepareOfferCommand(
            id,
            request.GrossSalary,
            request.Currency,
            request.ProposedStartDate,
            request.OfferExpiresAtUtc);

        var response = await sender.Send(
            command,
            cancellationToken);

        if (response.IsSuccess)
        {
            return NoContent();
        }

        var errorResponse = new
        {
            code = response.ErrorCode,
            errors = response.Errors
        };

        return response.ErrorCode switch
        {
            "HiringProcess.NotFound" =>
                NotFound(errorResponse),

            "HiringProcess.InvalidStatus" =>
                Conflict(errorResponse),

            _ => BadRequest(errorResponse)
        };
    }

    [HttpPost("{id:guid}/offer/send")]
    [Authorize(Roles = NexusHrRoles.HiringEditors)]
    public async Task<IActionResult> SendOffer(
    Guid id,
    CancellationToken cancellationToken)
    {
        var command = new SendOfferCommand(id);

        var response = await sender.Send(
            command,
            cancellationToken);

        if (response.IsSuccess)
        {
            return NoContent();
        }

        var errorResponse = new
        {
            code = response.ErrorCode,
            errors = response.Errors
        };

        return response.ErrorCode switch
        {
            "HiringProcess.NotFound" =>
                NotFound(errorResponse),

            "HiringProcess.InvalidStatus" =>
                Conflict(errorResponse),

            _ => BadRequest(errorResponse)
        };
    }

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [Authorize(Roles = NexusHrRoles.HiringReaders)]
    public async Task<IActionResult> GetById(
    Guid id,
    CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new GetHiringProcessByIdQuery(id),
            cancellationToken);

        if (response is null)
        {
            return NotFound(new
            {
                code = "HiringProcess.NotFound",
                errors = new[]
                {
                "İşe alım süreci bulunamadı."
            }
            });
        }

        return Ok(response);
    }

    [HttpPost("active/by-candidates")]
    [Authorize(Roles = NexusHrRoles.HiringReaders)]
    public async Task<IActionResult>
    GetActiveByCandidateIds(
        GetActiveHiringProcessesRequest request,
        CancellationToken cancellationToken)
    {
        var candidateIds =
            request.CandidateIds?
                .Where(candidateId =>
                    candidateId != Guid.Empty)
                .Distinct()
                .ToArray()
            ?? Array.Empty<Guid>();

        if (candidateIds.Length > 100)
        {
            return BadRequest(new
            {
                code =
                    "HiringProcess.TooManyCandidates",
                errors = new[]
                {
                "Tek seferde en fazla 100 aday sorgulanabilir."
            }
            });
        }

        var response =
            await sender.Send(
                new GetActiveHiringProcessesByCandidateIdsQuery(
                    candidateIds),
                cancellationToken);

        return Ok(response);
    }
}