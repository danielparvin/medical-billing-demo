namespace Billing.Domain;

public class Bill
{
    public int Id { get; set; }
    public string PatientRef { get; set; } = "";
    public DateOnly DateOfService { get; set; }
    public BillStatus Status { get; set; } = BillStatus.Draft;
    public DateTime? ProcessedAtUtc { get; set; }
    public decimal? TotalAllowed { get; set; }
    public List<LineItem> Lines { get; set; } = [];
}
