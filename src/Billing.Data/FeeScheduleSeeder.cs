using Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Billing.Data;

/// <summary>Loads invented, synthetic fee schedule data. Idempotent.</summary>
public sealed class FeeScheduleSeeder(BillingDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.FeeSchedule.AnyAsync(ct))
        {
            return;
        }

        var start = new DateOnly(2025, 1, 1);
        var priceChange = new DateOnly(2026, 1, 1);

        db.FeeSchedule.AddRange(
            Entry("A1001", "Basic office visit", 80.00m, start),
            Entry("A1002", "Extended office visit", 115.00m, start, new DateOnly(2025, 12, 31)),
            Entry("A1002", "Extended office visit", 120.00m, priceChange),
            Entry("B2001", "Minor procedure", 250.00m, start),
            Entry("B2002", "Imaging study", 310.50m, start),
            Entry("C3001", "Lab panel", 45.25m, start),
            Entry("C3002", "Specialty lab", 78.90m, start),
            Entry("D4001", "Therapy session", 95.00m, start),
            Entry("D4002", "Injection", 33.35m, start));

        await db.SaveChangesAsync(ct);
    }

    private static FeeScheduleEntry Entry(string code, string description, decimal amount, DateOnly from, DateOnly? to = null) =>
        new()
        {
            ProcedureCode = code,
            Description = description,
            BaseAmount = amount,
            EffectiveFrom = from,
            EffectiveTo = to
        };
}
