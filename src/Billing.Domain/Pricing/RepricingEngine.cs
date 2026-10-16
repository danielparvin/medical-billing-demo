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

    public static RepricingResult Reprice(Bill bill, IReadOnlyDictionary<string, decimal> baseFees)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(baseFees);

        var working = bill.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => PriceBeforeDiscount(l, baseFees))
            .ToList();

        ApplyMultipleProcedureDiscount(working);

        var results = working.Select(Finish).ToList();
        return new RepricingResult(results, results.Sum(r => r.AllowedAmount));
    }

    private static WorkingLine PriceBeforeDiscount(LineItem line, IReadOnlyDictionary<string, decimal> baseFees)
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

    public const decimal SecondaryProcedureFactor = 0.50m;

    /// <summary>
    /// Ranks priced lines by amount (highest first, ties by lowest line number).
    /// Rank 1 pays in full; every other rank pays <see cref="SecondaryProcedureFactor"/>.
    /// </summary>
    private static void ApplyMultipleProcedureDiscount(List<WorkingLine> working)
    {
        var ranked = working
            .Where(w => w.Amount is not null)
            .OrderByDescending(w => w.Amount)
            .ThenBy(w => w.Line.LineNumber)
            .ToList();

        foreach (var line in ranked.Skip(1))
        {
            line.Amount *= SecondaryProcedureFactor;
            line.Steps.Add(new AdjustmentStep(
                StepNames.MultipleProcedureDiscount, SecondaryProcedureFactor, line.Amount!.Value));
        }
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
