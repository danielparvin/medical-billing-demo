using Billing.Data;
using Billing.Domain;
using Billing.Domain.Pricing;

namespace Billing.Api;

public sealed class ProcessBillService(
    IBillRepository bills,
    IFeeScheduleRepository fees,
    TimeProvider time,
    ILogger<ProcessBillService> logger)
{
    /// <summary>Returns null when the bill does not exist.</summary>
    public async Task<Bill?> ProcessAsync(int billId, CancellationToken ct = default)
    {
        var bill = await bills.GetAsync(billId, ct);
        if (bill is null)
        {
            return null;
        }

        var codes = bill.Lines.Select(l => l.ProcedureCode).Distinct().ToList();
        var baseFees = await fees.GetBaseFeesAsync(codes, bill.DateOfService, ct);
        var result = RepricingEngine.Reprice(bill, baseFees);

        foreach (var lineResult in result.Lines)
        {
            var line = bill.Lines.Single(l => l.LineNumber == lineResult.LineNumber);
            line.AllowedAmount = lineResult.AllowedAmount;
            line.PricingStatus = lineResult.Status;
            line.UnpricedReason = lineResult.UnpricedReason;

            line.Adjustments.Clear();
            var sequence = 1;
            foreach (var step in lineResult.Steps)
            {
                line.Adjustments.Add(new LineItemAdjustment
                {
                    Sequence = sequence++,
                    Step = step.Step,
                    Factor = step.Factor,
                    ResultingAmount = step.ResultingAmount
                });
            }
        }

        bill.TotalAllowed = result.TotalAllowed;
        bill.Status = BillStatus.Processed;
        bill.ProcessedAtUtc = time.GetUtcNow().UtcDateTime;

        await bills.SaveChangesAsync(ct);

        logger.LogInformation(
            "Processed bill {BillId} with {LineCount} lines, {UnpricedCount} unpriced",
            bill.Id, bill.Lines.Count, result.Lines.Count(l => l.Status == LinePricingStatus.Unpriced));

        return bill;
    }
}
