using Billing.Contracts;
using Billing.Data;
using Billing.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Billing.Api.Controllers;

[ApiController]
[Route("api/bills")]
public class BillsController(IBillRepository bills, ProcessBillService processor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BillSummaryDto>>> List(CancellationToken ct)
    {
        var all = await bills.ListAsync(ct);
        return all.Select(BillMapper.ToSummary).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BillDto>> Get(int id, CancellationToken ct)
    {
        var bill = await bills.GetAsync(id, ct);
        return bill is null ? NotFound() : BillMapper.ToDto(bill);
    }

    [HttpPost]
    public async Task<ActionResult<BillDto>> Create(BillRequest request, CancellationToken ct)
    {
        var bill = new Bill();
        BillMapper.Apply(bill, request);
        bills.Add(bill);
        await bills.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = bill.Id }, BillMapper.ToDto(bill));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BillDto>> Update(int id, BillRequest request, CancellationToken ct)
    {
        var bill = await bills.GetAsync(id, ct);
        if (bill is null)
        {
            return NotFound();
        }

        BillMapper.Apply(bill, request);
        await bills.SaveChangesAsync(ct);
        return BillMapper.ToDto(bill);
    }

    [HttpPost("{id:int}/process")]
    public async Task<ActionResult<BillDto>> Process(int id, CancellationToken ct)
    {
        var bill = await processor.ProcessAsync(id, ct);
        return bill is null ? NotFound() : BillMapper.ToDto(bill);
    }
}
