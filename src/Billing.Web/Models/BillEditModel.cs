using Billing.Contracts;

namespace Billing.Web.Models;

public sealed class BillEditModel
{
    public string PatientRef { get; set; } = "";
    public DateOnly? DateOfService { get; set; }
    public List<LineEditModel> Lines { get; } = [];

    public static BillEditModel Empty()
    {
        var model = new BillEditModel();
        model.AddLine();
        return model;
    }

    public static BillEditModel FromDto(BillDto dto)
    {
        var model = new BillEditModel { PatientRef = dto.PatientRef, DateOfService = dto.DateOfService };
        foreach (var line in dto.Lines)
        {
            model.Lines.Add(new LineEditModel
            {
                LineNumber = line.LineNumber,
                ProcedureCode = line.ProcedureCode,
                Modifier50 = line.Modifiers.Contains("50"),
                Modifier26 = line.Modifiers.Contains("26"),
                Units = line.Units,
                BilledAmount = line.BilledAmount
            });
        }

        return model;
    }

    public void AddLine() =>
        Lines.Add(new LineEditModel { LineNumber = Lines.Count == 0 ? 1 : Lines.Max(l => l.LineNumber) + 1 });

    public void RemoveLineAt(int index) => Lines.RemoveAt(index);

    public BillRequest ToRequest() => new()
    {
        PatientRef = PatientRef,
        DateOfService = DateOfService,
        Lines = Lines.Select(l => l.ToRequest()).ToList()
    };
}

public sealed class LineEditModel
{
    public int LineNumber { get; set; }
    public string ProcedureCode { get; set; } = "";
    public bool Modifier50 { get; set; }
    public bool Modifier26 { get; set; }
    public int Units { get; set; } = 1;
    public decimal BilledAmount { get; set; }

    public LineItemRequest ToRequest()
    {
        var modifiers = new List<string>();
        if (Modifier50) modifiers.Add("50");
        if (Modifier26) modifiers.Add("26");

        return new LineItemRequest
        {
            LineNumber = LineNumber,
            ProcedureCode = ProcedureCode,
            Modifiers = modifiers,
            Units = Units,
            BilledAmount = BilledAmount
        };
    }
}
