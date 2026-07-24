using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusHR.BuildingBlocks.Security;
using NexusHR.Hiring.Api.Contracts.HiringProcesses;
using NexusHR.Hiring.Application.HiringProcesses.CreateHiringProcess;

namespace NexusHR.Hiring.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/hiring-processes")]
public sealed class HiringProcessesController(
    ISender sender)
    : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = NexusHrRoles.HrSpecialist)]
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

        return Created(
            $"/api/hiring-processes/{response.HiringProcessId}",
            new
            {
                id = response.HiringProcessId
            });
    }
}