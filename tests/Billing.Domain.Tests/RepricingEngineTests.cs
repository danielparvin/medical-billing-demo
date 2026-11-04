using Billing.Domain;
using Billing.Domain.Pricing;

namespace Billing.Domain.Tests;

[Trait("Category", "Unit")]
public class RepricingEngineTests
{
    [Fact]
    public void Reprice_sets_the_allowed_amount_to_the_base_fee_times_the_units()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", billed: 500m, units: 2).Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        var line = Assert.Single(result.Lines);
        Assert.Equal(LinePricingStatus.Priced, line.Status);
        Assert.Equal(200.00m, line.AllowedAmount);
    }

    [Fact]
    public void Reprice_pays_one_and_a_half_times_the_fee_when_the_line_has_the_bilateral_modifier()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(150.00m, result.Lines[0].AllowedAmount);
    }

    [Fact]
    public void Reprice_pays_forty_percent_of_the_fee_when_the_line_has_the_professional_component_modifier()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "26").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(40.00m, result.Lines[0].AllowedAmount);
    }

    [Fact]
    public void Reprice_combines_the_multipliers_when_the_line_has_both_the_bilateral_and_professional_component_modifiers()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50", "26").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(60.00m, result.Lines[0].AllowedAmount);
    }

    [Fact]
    public void Reprice_applies_a_modifier_only_once_when_the_line_lists_it_twice()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50", "50").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(150.00m, result.Lines[0].AllowedAmount);
    }

    [Fact]
    public void Reprice_rounds_away_from_zero_when_the_amount_lands_on_a_half_cent()
    {
        // 10.03 x 1.50 = 15.045 exactly; banker's rounding would give 15.04.
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 10.03m)));

        Assert.Equal(15.05m, result.Lines[0].AllowedAmount);
    }

    [Fact]
    public void Reprice_caps_the_allowed_amount_at_billed_charges_when_the_priced_amount_exceeds_them()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", billed: 80m).Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(80.00m, result.Lines[0].AllowedAmount);
        Assert.Contains(result.Lines[0].Steps, s => s.Step == StepNames.BilledChargesCap);
    }

    [Fact]
    public void Reprice_caps_the_allowed_amount_at_zero_when_the_billed_amount_is_zero()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", billed: 0m).Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(0.00m, result.Lines[0].AllowedAmount);
        Assert.Equal(LinePricingStatus.Priced, result.Lines[0].Status);
    }

    [Fact]
    public void Reprice_marks_the_line_unpriced_with_zero_allowed_when_the_procedure_code_has_no_fee_schedule_entry()
    {
        var bill = new BillBuilder().WithLine(1, "Z9999", 100m).Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        var line = Assert.Single(result.Lines);
        Assert.Equal(LinePricingStatus.Unpriced, line.Status);
        Assert.Equal(UnpricedReasons.NoFeeScheduleEntry, line.UnpricedReason);
        Assert.Equal(0.00m, line.AllowedAmount);
    }

    [Fact]
    public void Reprice_counts_unpriced_lines_as_zero_in_the_total_allowed_when_a_bill_has_priced_and_unpriced_lines()
    {
        var bill = new BillBuilder()
            .WithLine(1, "A1001", 500m)
            .WithLine(2, "Z9999", 500m)
            .Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(100.00m, result.TotalAllowed);
    }

    [Fact]
    public void Reprice_pays_every_line_in_full_and_sums_the_total_allowed_when_a_bill_has_several_priced_lines()
    {
        var bill = new BillBuilder()
            .WithLine(1, "A1001", 1000m)
            .WithLine(2, "B2001", 1000m)
            .WithLine(3, "C3001", 1000m)
            .Build();

        var result = RepricingEngine.Reprice(bill,
            BillBuilder.Fees(("A1001", 100.00m), ("B2001", 200.00m), ("C3001", 300.00m)));

        Assert.Equal([100.00m, 200.00m, 300.00m], result.Lines.Select(l => l.AllowedAmount));
        Assert.Equal(600.00m, result.TotalAllowed);
    }

    [Fact]
    public void Reprice_returns_no_lines_and_a_zero_total_when_the_bill_is_empty()
    {
        var result = RepricingEngine.Reprice(new Bill(), BillBuilder.Fees());

        Assert.Empty(result.Lines);
        Assert.Equal(0.00m, result.TotalAllowed);
    }

    [Fact]
    public void Reprice_records_each_audit_trail_step_in_order()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 2, "50").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

        Assert.Equal(
            [StepNames.BaseFee, StepNames.Units, StepNames.Modifier50, StepNames.Rounding],
            result.Lines[0].Steps.Select(s => s.Step));
        Assert.Equal(300.00m, result.Lines[0].Steps[^1].ResultingAmount);
    }

    [Fact]
    public void Reprice_returns_lines_in_line_number_order_when_the_input_is_out_of_order()
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
    public void Reprice_throws_an_ArgumentException_when_a_line_has_an_unknown_modifier()
    {
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "99").Build();

        Assert.Throws<ArgumentException>(() =>
            RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m))));
    }
}
