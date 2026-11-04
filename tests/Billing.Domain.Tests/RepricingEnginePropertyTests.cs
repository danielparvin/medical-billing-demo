using Billing.Domain;
using Billing.Domain.Pricing;
using CsCheck;

namespace Billing.Domain.Tests;

/// <summary>
/// Rules that must hold for any bill, checked against thousands of generated bills.
/// On failure, CsCheck reports the smallest failing bill it can find and the seed that reproduces it.
/// </summary>
[Trait("Category", "Unit")]
public class RepricingEnginePropertyTests
{
    private const long Iterations = 10_000;

    private static readonly Gen<decimal> Money = Gen.Int[0, 1_000_000].Select(cents => cents / 100m);

    private static readonly string[][] ModifierCombinations = [[], ["50"], ["26"], ["50", "26"]];

    private static readonly Gen<string[]> Modifiers = Gen.Int[0, 3].Select(i => ModifierCombinations[i]);

    private static readonly Gen<(int Units, decimal Billed, string[] Modifiers, decimal Fee)> Lines =
        Gen.Select(Gen.Int[1, 999], Money, Modifiers, Money);

    private static readonly Gen<(int Units, decimal Billed, string[] Modifiers, decimal Fee)[]> LineLists =
        Lines.Array[1, 10];

    private static (Bill Bill, IReadOnlyDictionary<string, decimal> Fees) BillFrom(
        IEnumerable<(int Units, decimal Billed, string[] Modifiers, decimal Fee)> lines)
    {
        var builder = new BillBuilder();
        var fees = new Dictionary<string, decimal>();
        var number = 0;
        foreach (var line in lines)
        {
            number++;
            var code = $"C{number:D4}";
            builder.WithLine(number, code, line.Billed, line.Units, line.Modifiers);
            fees[code] = line.Fee;
        }

        return (builder.Build(), fees);
    }

    [Fact]
    [Trait("Requirement", "PROC-2.6")]
    public void Reprice_never_allows_more_than_the_billed_amount()
    {
        LineLists.Sample(lines =>
        {
            var (bill, fees) = BillFrom(lines);

            var result = RepricingEngine.Reprice(bill, fees);

            return result.Lines.All(r => r.AllowedAmount <= bill.Lines.Single(l => l.LineNumber == r.LineNumber).BilledAmount);
        }, iter: Iterations);
    }

    [Fact]
    [Trait("Requirement", "PROC-3")]
    public void Reprice_sets_the_total_allowed_to_the_sum_of_the_line_amounts()
    {
        LineLists.Sample(lines =>
        {
            var (bill, fees) = BillFrom(lines);

            var result = RepricingEngine.Reprice(bill, fees);

            return result.TotalAllowed == result.Lines.Sum(l => l.AllowedAmount);
        }, iter: Iterations);
    }

    [Fact]
    [Trait("Requirement", "PROC-5.1")]
    public void Reprice_returns_the_same_result_whatever_order_the_lines_arrive_in()
    {
        LineLists.Sample(lines =>
        {
            var (bill, fees) = BillFrom(lines);
            var reversed = new Bill { PatientRef = bill.PatientRef, DateOfService = bill.DateOfService, Lines = bill.Lines.AsEnumerable().Reverse().ToList() };

            var expected = RepricingEngine.Reprice(bill, fees);
            var actual = RepricingEngine.Reprice(reversed, fees);

            return expected.TotalAllowed == actual.TotalAllowed
                && expected.Lines.Select(l => (l.LineNumber, l.AllowedAmount)).SequenceEqual(actual.Lines.Select(l => (l.LineNumber, l.AllowedAmount)));
        }, iter: Iterations);
    }
}
