using Billing.Contracts;
using Billing.Data;
using Microsoft.AspNetCore.Mvc;

namespace Billing.Api.Controllers;

[ApiController]
[Route("api/fee-schedule")]
public class FeeScheduleController(IFeeScheduleRepository fees) : ControllerBase
{
    [HttpGet("{procedureCode}")]
    public async Task<ActionResult<FeeDto>> Get(string procedureCode, [FromQuery] DateOnly? dateOfService, CancellationToken ct)
    {
        if (dateOfService is null)
        {
            ModelState.AddModelError(nameof(dateOfService), "dateOfService is required.");
            return ValidationProblem(ModelState);
        }

        var entry = await fees.FindAsync(procedureCode.ToUpperInvariant(), dateOfService.Value, ct);
        return entry is null
            ? NotFound()
            : new FeeDto(entry.ProcedureCode, entry.Description, entry.BaseAmount, entry.EffectiveFrom, entry.EffectiveTo);
    }
}
