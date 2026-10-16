using Billing.Domain;
using Billing.Domain.Pricing;

namespace Billing.Domain.Tests;

[Trait("Category", "Unit")]
public class MultipleProcedureDiscountTests
{
    [Fact]
    public void SingleLine_IsNotDiscounted()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 1000m).Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(100.00m, result.Lines[0].AllowedAmount);
        Assert.DoesNotContain(result.Lines[0].Steps, s => s.Step == StepNames.MultipleProcedureDiscount);
    }

    [Fact]
    public void HighestPricedLine_PaysInFull_AndTheOtherPaysHalf()
    {
        var bill = new BillBuilder()
            .WithLine(1, "A1001", 1000m)
            .WithLine(2, "B2001", 1000m)
            .Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m), ("B2001", 200.00m)));

        Assert.Equal(50.00m, result.Lines[0].AllowedAmount);
        Assert.Equal(200.00m, result.Lines[1].AllowedAmount);
        Assert.Equal(250.00m, result.TotalAllowed);
    }

    [Fact]
    public void ThreeLines_EveryNonTopRankPaysHalf()
    {
        var bill = new BillBuilder()
            .WithLine(1, "A1001", 1000m)
            .WithLine(2, "B2001", 1000m)
            .WithLine(3, "C3001", 1000m)
            .Build();

        var result = RepricingEngine.Reprice(bill,
            BillBuilder.Fees(("A1001", 100.00m), ("B2001", 200.00m), ("C3001", 300.00m)));

        Assert.Equal([50.00m, 100.00m, 300.00m], result.Lines.Select(l => l.AllowedAmount));
        Assert.Equal(450.00m, result.TotalAllowed);
    }

    [Fact]
    public void TiedAmounts_RankByLowestLineNumber()
    {
        var bill = new BillBuilder()
            .WithLine(2, "B2001", 1000m)
            .WithLine(1, "A1001", 1000m)
            .Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m), ("B2001", 100.00m)));

        Assert.Equal(100.00m, result.Lines.Single(l => l.LineNumber == 1).AllowedAmount);
        Assert.Equal(50.00m, result.Lines.Single(l => l.LineNumber == 2).AllowedAmount);
    }

    [Fact]
    public void UnpricedLines_AreExcludedFromRanking()
    {
        var bill = new BillBuilder()
            .WithLine(1, "Z9999", 1000m)
            .WithLine(2, "A1001", 1000m)
            .Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(100.00m, result.Lines.Single(l => l.LineNumber == 2).AllowedAmount);
    }

    [Fact]
    public void Ranking_UsesTheAmountAfterUnitsAndModifiers()
    {
        // Line 1 base fee is higher, but line 2 with the bilateral modifier is worth more (150 vs 140).
        var bill = new BillBuilder()
            .WithLine(1, "A1001", 1000m)
            .WithLine(2, "B2001", 1000m, 1, "50")
            .Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 140.00m), ("B2001", 100.00m)));

        Assert.Equal(70.00m, result.Lines.Single(l => l.LineNumber == 1).AllowedAmount);
        Assert.Equal(150.00m, result.Lines.Single(l => l.LineNumber == 2).AllowedAmount);
    }

    [Fact]
    public void DiscountedHalfCent_RoundsAwayFromZero()
    {
        // 20.01 x 0.50 = 10.005 exactly; banker's rounding would give 10.00.
        var bill = new BillBuilder()
            .WithLine(1, "A1001", 1000m)
            .WithLine(2, "B2001", 1000m)
            .Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m), ("B2001", 20.01m)));

        Assert.Equal(10.01m, result.Lines.Single(l => l.LineNumber == 2).AllowedAmount);
    }

    [Fact]
    public void DiscountedAmount_IsStillCappedAtBilledCharges()
    {
        var bill = new BillBuilder()
            .WithLine(1, "A1001", 1000m)
            .WithLine(2, "B2001", billed: 30m)
            .Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m), ("B2001", 100.00m)));

        Assert.Equal(30.00m, result.Lines.Single(l => l.LineNumber == 2).AllowedAmount);
    }

    [Fact]
    public void DiscountedLine_RecordsTheDiscountStep()
    {
        var bill = new BillBuilder()
            .WithLine(1, "A1001", 1000m)
            .WithLine(2, "B2001", 1000m)
            .Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m), ("B2001", 200.00m)));

        var step = Assert.Single(result.Lines[0].Steps, s => s.Step == StepNames.MultipleProcedureDiscount);
        Assert.Equal(0.50m, step.Factor);
        Assert.Equal(50.00m, step.ResultingAmount);
    }

    [Fact]
    public void ResultsAreIdentical_WhenTheLinesAreSuppliedInADifferentOrder()
    {
        var fees = BillBuilder.Fees(("A1001", 100.00m), ("B2001", 200.00m), ("C3001", 100.00m));
        var forward = new BillBuilder()
            .WithLine(1, "A1001", 1000m).WithLine(2, "B2001", 1000m).WithLine(3, "C3001", 1000m).Build();
        var shuffled = new BillBuilder()
            .WithLine(3, "C3001", 1000m).WithLine(1, "A1001", 1000m).WithLine(2, "B2001", 1000m).Build();

        var a = RepricingEngine.Reprice(forward, fees);
        var b = RepricingEngine.Reprice(shuffled, fees);

        Assert.Equal(
            a.Lines.Select(l => (l.LineNumber, l.AllowedAmount)),
            b.Lines.Select(l => (l.LineNumber, l.AllowedAmount)));
        Assert.Equal(a.TotalAllowed, b.TotalAllowed);
    }
}
