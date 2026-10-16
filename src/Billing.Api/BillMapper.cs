using Billing.Contracts;
using Billing.Domain;

namespace Billing.Api;

internal static class BillMapper
{
    public static BillDto ToDto(Bill bill) => new(
        bill.Id,
        bill.PatientRef,
        bill.DateOfService,
        bill.Status.ToString(),
        bill.ProcessedAtUtc,
        bill.TotalAllowed,
        bill.Lines.OrderBy(l => l.LineNumber).Select(ToDto).ToList());

    public static BillSummaryDto ToSummary(Bill bill) =>
        new(bill.Id, bill.PatientRef, bill.DateOfService, bill.Status.ToString(), bill.TotalAllowed);

    /// <summary>Replaces header and lines, clears every processing result, and returns the bill to Draft.</summary>
    public static void Apply(Bill bill, BillRequest request)
    {
        bill.PatientRef = request.PatientRef;
        bill.DateOfService = request.DateOfService!.Value;
        bill.Status = BillStatus.Draft;
        bill.ProcessedAtUtc = null;
        bill.TotalAllowed = null;

        bill.Lines.Clear();
        foreach (var line in request.Lines)
        {
            bill.Lines.Add(new LineItem
            {
                LineNumber = line.LineNumber,
                ProcedureCode = line.ProcedureCode.ToUpperInvariant(),
                Modifiers = line.Modifiers.ToList(),
                Units = line.Units,
                BilledAmount = line.BilledAmount
            });
        }
    }

    private static LineItemDto ToDto(LineItem line) => new(
        line.LineNumber,
        line.ProcedureCode,
        line.Modifiers.ToList(),
        line.Units,
        line.BilledAmount,
        line.AllowedAmount,
        line.PricingStatus?.ToString(),
        line.UnpricedReason,
        line.Adjustments
            .OrderBy(a => a.Sequence)
            .Select(a => new AdjustmentDto(a.Sequence, a.Step, a.Factor, a.ResultingAmount))
            .ToList());
}
