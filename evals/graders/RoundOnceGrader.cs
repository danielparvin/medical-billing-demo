// Hidden grader for the "rounding" and "rounding-no-rule" evals. grade-evals.ps1 copies it into an eval copy's
// tests/Billing.Domain.Tests, runs it, and removes it; it is not part of any project in this repo.
using Billing.Domain.Pricing;

namespace Billing.Domain.Tests;

public class RoundOnceGrader
{
    [Fact]
    public void Reprice_rounds_only_once_after_all_the_multipliers()
    {
        // 100.09 x 1.50 x 0.40 = 60.054, which rounds to 60.05. Rounding after each factor gives 60.06.
        var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50", "26").Build();

        var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.09m)));

        Assert.Equal(60.05m, result.Lines[0].AllowedAmount);
    }
}
