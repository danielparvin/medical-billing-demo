namespace Billing.Domain;

public class LineItem
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public int LineNumber { get; set; }
    public string ProcedureCode { get; set; } = "";
    public List<string> Modifiers { get; set; } = [];
    public int Units { get; set; }
    public decimal BilledAmount { get; set; }
    public decimal? AllowedAmount { get; set; }
    public LinePricingStatus? PricingStatus { get; set; }
    public string? UnpricedReason { get; set; }
    public List<LineItemAdjustment> Adjustments { get; set; } = [];
}
