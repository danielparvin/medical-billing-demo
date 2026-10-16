namespace Billing.Domain.Pricing;

public sealed record AdjustmentStep(string Step, decimal? Factor, decimal ResultingAmount);

public sealed record LineResult(
    int LineNumber,
    LinePricingStatus Status,
    string? UnpricedReason,
    decimal AllowedAmount,
    IReadOnlyList<AdjustmentStep> Steps);

public sealed record RepricingResult(IReadOnlyList<LineResult> Lines, decimal TotalAllowed);
