using Billing.Contracts;

namespace Billing.Integration.Tests;

internal static class TestBills
{
    public static BillRequest Request(params LineItemRequest[] lines) => new()
    {
        PatientRef = "TEST-0001",
        DateOfService = new DateOnly(2026, 3, 1),
        Lines = lines.ToList()
    };

    public static LineItemRequest Line(int number, string code, decimal billed, int units = 1, params string[] modifiers) => new()
    {
        LineNumber = number,
        ProcedureCode = code,
        BilledAmount = billed,
        Units = units,
        Modifiers = modifiers.ToList()
    };
}
