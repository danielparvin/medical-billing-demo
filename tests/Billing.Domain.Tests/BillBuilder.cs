using Billing.Domain;

namespace Billing.Domain.Tests;

internal sealed class BillBuilder
{
    private readonly List<LineItem> _lines = [];
    private DateOnly _dateOfService = new(2026, 3, 1);

    public BillBuilder OnDate(DateOnly dateOfService)
    {
        _dateOfService = dateOfService;
        return this;
    }

    public BillBuilder WithLine(int number, string code, decimal billed, int units = 1, params string[] modifiers)
    {
        _lines.Add(new LineItem
        {
            LineNumber = number,
            ProcedureCode = code,
            BilledAmount = billed,
            Units = units,
            Modifiers = modifiers.ToList()
        });
        return this;
    }

    public Bill Build() => new()
    {
        PatientRef = "TEST-0001",
        DateOfService = _dateOfService,
        Lines = _lines.ToList()
    };

    public static IReadOnlyDictionary<string, decimal> Fees(params (string Code, decimal Fee)[] fees) =>
        fees.ToDictionary(f => f.Code, f => f.Fee);
}
