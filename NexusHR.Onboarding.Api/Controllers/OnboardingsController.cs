using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusHR.BuildingBlocks.Security;
using NexusHR.Onboarding.Application.EmployeeOnboardings.GetEmployeeOnboardingById;
using NexusHR.Onboarding.Application.EmployeeOnboardings.GetEmployeeOnboardings;
using NexusHR.Onboarding.Application.EmployeeOnboardings.UpdateOnboarding;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Api.Controllers;

[ApiController]
[Authorize(Roles = NexusHrRoles.OnboardingReaders)]
[Route("api/onboardings")]
public sealed class OnboardingsController(
    ISender sender)
    : ControllerBase
{
    [Authorize(Roles = NexusHrRoles.OnboardingEditors)]
    [HttpPut("{id:guid}/tasks/{taskId:guid}/status")]
    public async Task<IActionResult> UpdateTask(Guid id, Guid taskId,
        UpdateTaskStatusRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateOnboardingCommand(id, taskId, request.Status), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = NexusHrRoles.HrSpecialist)]
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateOnboardingCommand(id, null, null), cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new GetEmployeeOnboardingsQuery(),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new GetEmployeeOnboardingByIdQuery(id),
            cancellationToken);

        if (response is null)
        {
            return NotFound(new
            {
                code = "Onboarding.NotFound",
                errors = new[]
                {
                    "Çalışan onboarding kaydı bulunamadı."
                }
            });
        }

        return Ok(response);
    }
}

public sealed record UpdateTaskStatusRequest(OnboardingTaskStatus Status);
