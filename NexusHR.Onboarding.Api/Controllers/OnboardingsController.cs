using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusHR.BuildingBlocks.Security;
using NexusHR.Onboarding.Application.EmployeeOnboardings.GetEmployeeOnboardingById;
using NexusHR.Onboarding.Application.EmployeeOnboardings.GetEmployeeOnboardings;

namespace NexusHR.Onboarding.Api.Controllers;

[ApiController]
[Authorize(Roles = NexusHrRoles.OnboardingReaders)]
[Route("api/onboardings")]
public sealed class OnboardingsController(
    ISender sender)
    : ControllerBase
{
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
