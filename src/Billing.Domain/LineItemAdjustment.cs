namespace Billing.Domain;

public class LineItemAdjustment
{
    public int Id { get; set; }
    public int LineItemId { get; set; }
    public int Sequence { get; set; }
    public string Step { get; set; } = "";
    public decimal? Factor { get; set; }
    public decimal ResultingAmount { get; set; }
}
