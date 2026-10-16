using System.ComponentModel.DataAnnotations;

namespace Billing.Contracts;

public sealed class BillRequest : IValidatableObject
{
    public static readonly IReadOnlySet<string> AllowedModifiers = new HashSet<string> { "50", "26" };

    [Required, StringLength(50, MinimumLength = 1)]
    public string PatientRef { get; init; } = "";

    [Required]
    public DateOnly? DateOfService { get; init; }

    [Required, MinLength(1)]
    public List<LineItemRequest> Lines { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DateOfService == DateOnly.MinValue)
        {
            yield return new ValidationResult("DateOfService must be a real date.", new[] { nameof(DateOfService) });
        }

        var lines = Lines ?? [];

        foreach (var duplicate in lines.GroupBy(l => l.LineNumber).Where(g => g.Count() > 1))
        {
            yield return new ValidationResult(
                $"Line number {duplicate.Key} is used more than once.", new[] { nameof(Lines) });
        }

        foreach (var line in lines)
        {
            var modifiers = line.Modifiers ?? [];

            foreach (var unknown in modifiers.Where(m => !AllowedModifiers.Contains(m)))
            {
                yield return new ValidationResult(
                    $"Line {line.LineNumber}: unknown modifier '{unknown}'.", new[] { nameof(Lines) });
            }

            if (modifiers.Distinct().Count() != modifiers.Count)
            {
                yield return new ValidationResult(
                    $"Line {line.LineNumber}: a modifier is listed more than once.", new[] { nameof(Lines) });
            }
        }
    }
}
