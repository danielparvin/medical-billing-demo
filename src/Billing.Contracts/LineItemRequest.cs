using System.ComponentModel.DataAnnotations;

namespace Billing.Contracts;

public sealed class LineItemRequest
{
    [Range(1, int.MaxValue)]
    public int LineNumber { get; init; }

    [Required, StringLength(5, MinimumLength = 5)]
    public string ProcedureCode { get; init; } = "";

    public List<string> Modifiers { get; init; } = [];

    [Range(1, 999)]
    public int Units { get; init; }

    [Range(typeof(decimal), "0", "9999999.99")]
    public decimal BilledAmount { get; init; }
}
