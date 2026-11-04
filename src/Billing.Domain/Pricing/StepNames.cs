namespace Billing.Domain.Pricing;

public static class StepNames
{
    public const string BaseFee = "BaseFee";
    public const string Units = "Units";
    public const string Modifier50 = "Modifier50";
    public const string Modifier26 = "Modifier26";
    public const string Rounding = "Rounding";
    public const string BilledChargesCap = "BilledChargesCap";
}

public static class UnpricedReasons
{
    public const string NoFeeScheduleEntry = "NoFeeScheduleEntry";
}
