using Billing.Domain;
using Billing.Domain.Pricing;

namespace Billing.Domain.Tests;

[Trait("Category", "Unit")]
public class RepricingEngineTests
{
    [Fact]
    public void PricedLine_IsBaseFeeTimesUnits()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", billed: 500m, units: 2).Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        var line = Assert.Single(result.Lines);
        Assert.Equal(LinePricingStatus.Priced, line.Status);
        Assert.Equal(200.00m, line.AllowedAmount);
    }

    [Fact]
    public void BilateralModifier_PaysOneAndAHalfTimes()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(150.00m, result.Lines[0].AllowedAmount);
    }

    [Fact]
    public void ProfessionalComponentModifier_PaysFortyPercent()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "26").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(40.00m, result.Lines[0].AllowedAmount);
    }

    [Fact]
    public void BothModifiers_CombineTheirMultipliers()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50", "26").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(60.00m, result.Lines[0].AllowedAmount);
    }

    [Fact]
    public void DuplicateModifier_IsAppliedOnlyOnce()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50", "50").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(150.00m, result.Lines[0].AllowedAmount);
    }

    [Fact]
    public void HalfCentAmounts_RoundAwayFromZero()
    {
        // 10.03 x 1.50 = 15.045 exactly; banker's rounding would give 15.04.
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 10.03m)));

        Assert.Equal(15.05m, result.Lines[0].AllowedAmount);
    }

    [Fact]
    public void AllowedAmount_IsCappedAtBilledCharges()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", billed: 80m).Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(80.00m, result.Lines[0].AllowedAmount);
        Assert.Contains(result.Lines[0].Steps, s => s.Step == StepNames.BilledChargesCap);
    }

    [Fact]
    public void ZeroBilledAmount_CapsAllowedAtZero()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", billed: 0m).Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(0.00m, result.Lines[0].AllowedAmount);
        Assert.Equal(LinePricingStatus.Priced, result.Lines[0].Status);
    }

    [Fact]
    public void LineWithNoFeeScheduleEntry_IsUnpricedWithZeroAllowed()
    {
        var bill = new BillBuilder().WithLine(1, "Z9999", 100m).Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        var line = Assert.Single(result.Lines);
        Assert.Equal(LinePricingStatus.Unpriced, line.Status);
        Assert.Equal(UnpricedReasons.NoFeeScheduleEntry, line.UnpricedReason);
        Assert.Equal(0.00m, line.AllowedAmount);
    }

    [Fact]
    public void TotalAllowed_IsTheSumOfLineAllowedAmounts_UnpricedLinesContributeZero()
    {
        var bill = new BillBuilder()
            .WithLine(1, "A1001", 500m)
            .WithLine(2, "Z9999", 500m)
            .Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(100.00m, result.TotalAllowed);
    }

    [Fact]
    public void EmptyBill_HasAZeroTotal()
    {
        var result = RepricingEngine.Reprice(new Bill(), BillBuilder.Fees());

        Assert.Empty(result.Lines);
        Assert.Equal(0.00m, result.TotalAllowed);
    }

    [Fact]
    public void AuditTrail_ListsEachStepInOrder()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 2, "50").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(
            [StepNames.BaseFee, StepNames.Units, StepNames.Modifier50, StepNames.Rounding],
            result.Lines[0].Steps.Select(s => s.Step));
        Assert.Equal(300.00m, result.Lines[0].Steps[^1].ResultingAmount);
    }

    [Fact]
    public void Results_AreOrderedByLineNumber_RegardlessOfInputOrder()
    {
        var bill = new BillBuilder()
            .WithLine(3, "A1001", 500m)
            .WithLine(1, "A1001", 500m)
            .WithLine(2, "A1001", 500m)
            .Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal([1, 2, 3], result.Lines.Select(l => l.LineNumber));
    }

    [Fact]
    public void UnknownModifier_IsRejected()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "99").Build();

        Assert.Throws<ArgumentException>(() =>
            RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m))));
    }
}
