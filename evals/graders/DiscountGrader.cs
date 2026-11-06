// Hidden grader for the "discount" eval. grade-evals.ps1 copies it into an eval copy's tests/Billing.Domain.Tests,
// runs it, and removes it; it is not part of any project in this repo.
// All six checks pass on tag cp-5-mpd-complete; the first, second, fourth and sixth fail on the engine without the discount.
using Billing.Domain.Pricing;

namespace Billing.Domain.Tests;

public class DiscountGrader
{
    private static decimal[] Allowed(BillBuilder builder, params (string Code, decimal Fee)[] fees) =>
        RepricingEngine.Reprice(builder.Build(), BillBuilder.Fees(fees)).Lines.Select(l => l.AllowedAmount).ToArray();

    [Fact]
    public void Reprice_pays_the_highest_line_in_full_and_the_other_at_half() =>
        Assert.Equal([50.00m, 200.00m],
            Allowed(new BillBuilder().WithLine(1, "A1001", 1000m).WithLine(2, "B2001", 1000m), ("A1001", 100m), ("B2001", 200m)));

    [Fact]
    public void Reprice_pays_the_lowest_line_number_in_full_when_amounts_tie() =>
        Assert.Equal([100.00m, 50.00m],
            Allowed(new BillBuilder().WithLine(1, "A1001", 1000m).WithLine(2, "B2001", 1000m), ("A1001", 100m), ("B2001", 100m)));

    [Fact]
    public void Reprice_leaves_unpriced_lines_out_of_the_ranking() =>
        Assert.Equal([0.00m, 100.00m],
            Allowed(new BillBuilder().WithLine(1, "Z9999", 1000m).WithLine(2, "A1001", 1000m), ("A1001", 100m)));

    [Fact]
    public void Reprice_rounds_once_after_the_discount() =>
        // 10.03 x 1.5 = 15.045, x 0.5 = 7.5225, which rounds to 7.52. Rounding before the discount gives 7.53.
        Assert.Equal([100.00m, 7.52m],
            Allowed(new BillBuilder().WithLine(1, "A1001", 1000m).WithLine(2, "B2001", 1000m, 1, "50"), ("A1001", 100m), ("B2001", 10.03m)));

    [Fact]
    public void Reprice_caps_the_discounted_amount_at_billed_charges() =>
        Assert.Equal([200.00m, 40.00m],
            Allowed(new BillBuilder().WithLine(1, "A1001", 1000m).WithLine(2, "B2001", 40m), ("A1001", 200m), ("B2001", 100m)));

    [Fact]
    public void Reprice_ranks_by_the_amount_after_modifiers() =>
        // Line 1: 100 x 0.4 = 40; line 2: 60. Line 2 ranks first.
        Assert.Equal([20.00m, 60.00m],
            Allowed(new BillBuilder().WithLine(1, "A1001", 1000m, 1, "26").WithLine(2, "B2001", 1000m), ("A1001", 100m), ("B2001", 60m)));
}
