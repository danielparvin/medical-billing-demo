namespace Billing.Domain;

public class FeeScheduleEntry
{
    public int Id { get; set; }
    public string ProcedureCode { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal BaseAmount { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}
