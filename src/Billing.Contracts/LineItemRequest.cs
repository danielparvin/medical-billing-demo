using System.ComponentModel.DataAnnotations;

namespace Billing.Contracts;

public sealed class LineItemRequest
{
    /// <summary>Exactly 5 ASCII letters or digits, in either case.</summary>
    public const string ProcedureCodePattern = "^[A-Za-z0-9]{5}$";

    private const string ProcedureCodeMessage = "ProcedureCode must be exactly 5 letters or digits.";

    [Range(1, int.MaxValue)]
    public int LineNumber { get; init; }

    // [RegularExpression] treats an empty string as valid and leaves it to [Required], so both give the same message.
    [Required(ErrorMessage = ProcedureCodeMessage)]
    [RegularExpression(ProcedureCodePattern, ErrorMessage = ProcedureCodeMessage)]
    public string ProcedureCode { get; init; } = "";

    public List<string> Modifiers { get; init; } = [];

    [Range(1, 999)]
    public int Units { get; init; }

    [Range(typeof(decimal), "0", "9999999.99")]
    public decimal BilledAmount { get; init; }
}
