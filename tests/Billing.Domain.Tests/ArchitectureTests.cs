using System.Reflection;
using Billing.Domain.Pricing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NetArchTest.Rules;

namespace Billing.Domain.Tests;

/// <summary>
/// PROC-5 as rules over the compiled Billing.Domain assembly, so they hold for every type the engine can reach,
/// including compiler-generated ones such as lambdas.
/// </summary>
[Trait("Category", "Unit")]
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(RepricingEngine).Assembly;

    [Fact]
    [Trait("Requirement", "PROC-5")]
    public void Reprice_depends_on_no_database_logging_or_time_library()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.Data", "System.Data",
                "Microsoft.Extensions.Logging", "System.Console", "System.Diagnostics.Trace",
                "System.TimeProvider", "Microsoft.Extensions.Time")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    [Trait("Requirement", "PROC-5")]
    public void Reprice_never_reads_the_system_clock()
    {
        var result = Types.InAssembly(Domain)
            .Should()
            .MeetCustomRule(new DoesNotReadTheSystemClock())
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        "These types break the rule: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []);

    /// <summary>
    /// <c>DateTime</c> itself is allowed (a bill records when it was processed), so a dependency rule can't catch
    /// <c>DateTime.Now</c>. This rule looks for calls to the clock in each method's IL instead.
    /// </summary>
    private sealed class DoesNotReadTheSystemClock : ICustomRule
    {
        private static readonly HashSet<string> ClockReads =
        [
            "System.DateTime::get_Now",
            "System.DateTime::get_UtcNow",
            "System.DateTime::get_Today",
            "System.DateTimeOffset::get_Now",
            "System.DateTimeOffset::get_UtcNow",
            "System.Environment::get_TickCount",
            "System.Environment::get_TickCount64",
            "System.Diagnostics.Stopwatch::GetTimestamp",
            "System.Diagnostics.Stopwatch::StartNew",
        ];

        // Lambdas and iterators compile into nested types that NetArchTest doesn't list on their own,
        // so each type is checked together with everything nested inside it.
        public bool MeetsRule(TypeDefinition type) =>
            !type.Methods
                .Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions)
                .Any(i => i.OpCode.FlowControl == FlowControl.Call
                    && i.Operand is MethodReference called
                    && ClockReads.Contains($"{called.DeclaringType.FullName}::{called.Name}"))
            && type.NestedTypes.All(MeetsRule);
    }
}
