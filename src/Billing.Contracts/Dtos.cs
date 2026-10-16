namespace Billing.Contracts;

public sealed record AdjustmentDto(int Sequence, string Step, decimal? Factor, decimal ResultingAmount);

public sealed record LineItemDto(
    int LineNumber,
    string ProcedureCode,
    IReadOnlyList<string> Modifiers,
    int Units,
    decimal BilledAmount,
    decimal? AllowedAmount,
    string? PricingStatus,
    string? UnpricedReason,
    IReadOnlyList<AdjustmentDto> Adjustments);

public sealed record BillDto(
    int Id,
    string PatientRef,
    DateOnly DateOfService,
    string Status,
    DateTime? ProcessedAtUtc,
    decimal? TotalAllowed,
    IReadOnlyList<LineItemDto> Lines);

public sealed record BillSummaryDto(int Id, string PatientRef, DateOnly DateOfService, string Status, decimal? TotalAllowed);

public sealed record FeeDto(
    string ProcedureCode, string Description, decimal BaseAmount, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
