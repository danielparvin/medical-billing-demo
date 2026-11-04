namespace Billing.Domain.Pricing;

/// <summary>
/// Pure, deterministic repricing. No database, clock, or logging.
/// Rounding rule: once per line, to 2 decimals, MidpointRounding.AwayFromZero, after all multipliers.
/// </summary>
public static class RepricingEngine
{
    public const decimal BilateralFactor = 1.50m;
    public const decimal ProfessionalComponentFactor = 0.40m;

    private static readonly HashSet<string> KnownModifiers = ["50", "26"];

    /// <summary>
    /// Prices every line of a bill from its base fee. The bill is not changed; the caller applies the result.
    /// </summary>
    /// <param name="bill">The bill whose lines are priced.</param>
    /// <param name="baseFees">
    /// Base fee by procedure code. These must already be the fees in effect on the bill's date of service,
    /// because the engine does not look at dates.
    /// </param>
    /// <returns>
    /// One <see cref="LineResult"/> per line, ordered by line number, with its audit trail in
    /// <see cref="LineResult.Steps"/>, and the sum of the allowed amounts in <see cref="RepricingResult.TotalAllowed"/>.
    /// </returns>
    /// <remarks>
    /// A line whose procedure code is not in <paramref name="baseFees"/> does not stop the others: it comes back as
    /// <see cref="LinePricingStatus.Unpriced"/> with <see cref="UnpricedReasons.NoFeeScheduleEntry"/>, an allowed amount
    /// of 0 and no steps. A priced line's amount is rounded once and then capped at its billed amount.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="bill"/> or <paramref name="baseFees"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Any line, priced or not, has a modifier other than 50 or 26. No partial result is returned.
    /// </exception>
    public static RepricingResult Reprice(Bill bill, IReadOnlyDictionary<string, decimal> baseFees)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(baseFees);

        var results = bill.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => Finish(PriceBeforeRounding(l, baseFees)))
            .ToList();

        return new RepricingResult(results, results.Sum(r => r.AllowedAmount));
    }

    private static WorkingLine PriceBeforeRounding(LineItem line, IReadOnlyDictionary<string, decimal> baseFees)
    {
        var unknown = line.Modifiers.FirstOrDefault(m => !KnownModifiers.Contains(m));
        if (unknown is not null)
        {
            throw new ArgumentException($"Line {line.LineNumber} has unknown modifier '{unknown}'.", nameof(line));
        }

        var working = new WorkingLine(line);

        if (!baseFees.TryGetValue(line.ProcedureCode, out var baseFee))
        {
            working.UnpricedReason = UnpricedReasons.NoFeeScheduleEntry;
            return working;
        }

        var amount = baseFee;
        working.Steps.Add(new AdjustmentStep(StepNames.BaseFee, null, amount));

        amount *= line.Units;
        working.Steps.Add(new AdjustmentStep(StepNames.Units, line.Units, amount));

        if (line.Modifiers.Contains("50"))
        {
            amount *= BilateralFactor;
            working.Steps.Add(new AdjustmentStep(StepNames.Modifier50, BilateralFactor, amount));
        }

        if (line.Modifiers.Contains("26"))
        {
            amount *= ProfessionalComponentFactor;
            working.Steps.Add(new AdjustmentStep(StepNames.Modifier26, ProfessionalComponentFactor, amount));
        }

        working.Amount = amount;
        return working;
    }

    private static LineResult Finish(WorkingLine working)
    {
        var line = working.Line;

        if (working.Amount is null)
        {
            return new LineResult(line.LineNumber, LinePricingStatus.Unpriced, working.UnpricedReason, 0m, working.Steps);
        }

        var rounded = Math.Round(working.Amount.Value, 2, MidpointRounding.AwayFromZero);
        working.Steps.Add(new AdjustmentStep(StepNames.Rounding, null, rounded));

        var allowed = rounded;
        if (rounded > line.BilledAmount)
        {
            allowed = line.BilledAmount;
            working.Steps.Add(new AdjustmentStep(StepNames.BilledChargesCap, null, allowed));
        }

        return new LineResult(line.LineNumber, LinePricingStatus.Priced, null, allowed, working.Steps);
    }

    private sealed class WorkingLine(LineItem line)
    {
        public LineItem Line { get; } = line;
        public decimal? Amount { get; set; }
        public string? UnpricedReason { get; set; }
        public List<AdjustmentStep> Steps { get; } = [];
    }
}
