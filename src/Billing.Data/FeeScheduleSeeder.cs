using Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Billing.Data;

/// <summary>
/// Loads invented, synthetic fee schedule data. Idempotent: adds only the entries whose procedure code and
/// effective-from date are not already present, so entries added here also reach an existing database.
/// </summary>
public sealed class FeeScheduleSeeder(BillingDbContext db)
{
    private static readonly DateOnly Start = new(2025, 1, 1);
    private static readonly DateOnly EndOf2025 = new(2025, 12, 31);
    private static readonly DateOnly PriceChange = new(2026, 1, 1);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existingEntryKeys = (await db.FeeSchedule
                .Select(e => new { e.ProcedureCode, e.EffectiveFrom })
                .ToListAsync(ct))
            .Select(e => (e.ProcedureCode, e.EffectiveFrom))
            .ToHashSet();

        var missingEntries = Entries()
            .Where(e => !existingEntryKeys.Contains((e.ProcedureCode, e.EffectiveFrom)))
            .ToList();
        if (missingEntries.Count == 0)
        {
            return;
        }

        db.FeeSchedule.AddRange(missingEntries);
        await db.SaveChangesAsync(ct);
    }

    private static FeeScheduleEntry[] Entries() =>
    [
        Entry("A1001", "Basic office visit", 80.00m, Start),
        Entry("A1002", "Extended office visit", 115.00m, Start, EndOf2025),
        Entry("A1002", "Extended office visit", 120.00m, PriceChange),
        Entry("A1003", "New patient office visit", 145.00m, Start),
        Entry("B1234", "Skin biopsy", 165.45m, Start),
        Entry("B1235", "Joint aspiration", 142.00m, Start),
        Entry("B1236", "Ultrasound study", 275.00m, Start, EndOf2025),
        Entry("B1236", "Ultrasound study", 289.50m, PriceChange),
        Entry("B1237", "Wound repair", 198.75m, Start),
        Entry("B2001", "Minor procedure", 250.00m, Start),
        Entry("B2002", "Imaging study", 310.50m, Start),
        Entry("C1234", "Metabolic panel", 28.40m, Start),
        Entry("C1235", "Complete blood count", 18.65m, Start),
        Entry("C1236", "Lipid panel", 36.15m, Start),
        Entry("C1237", "Urinalysis", 12.05m, Start),
        Entry("C3001", "Lab panel", 45.25m, Start),
        Entry("C3002", "Specialty lab", 78.90m, Start),
        Entry("D4001", "Therapy session", 95.00m, Start),
        Entry("D4002", "Injection", 33.35m, Start),
        Entry("D4003", "Therapy evaluation", 130.00m, Start)
    ];

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
