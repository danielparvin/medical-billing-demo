using Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Billing.Data;

public interface IFeeScheduleRepository
{
    Task<FeeScheduleEntry?> FindAsync(string procedureCode, DateOnly dateOfService, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, decimal>> GetBaseFeesAsync(
        IEnumerable<string> procedureCodes, DateOnly dateOfService, CancellationToken ct = default);
}

public sealed class FeeScheduleRepository(BillingDbContext db) : IFeeScheduleRepository
{
    public Task<FeeScheduleEntry?> FindAsync(string procedureCode, DateOnly dateOfService, CancellationToken ct = default) =>
        db.FeeSchedule.AsNoTracking()
            .Where(e => e.ProcedureCode == procedureCode
                        && e.EffectiveFrom <= dateOfService
                        && (e.EffectiveTo == null || e.EffectiveTo >= dateOfService))
            .OrderByDescending(e => e.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<string, decimal>> GetBaseFeesAsync(
        IEnumerable<string> procedureCodes, DateOnly dateOfService, CancellationToken ct = default)
    {
        var codes = procedureCodes.Distinct().ToList();

        var candidates = await db.FeeSchedule.AsNoTracking()
            .Where(e => codes.Contains(e.ProcedureCode)
                        && e.EffectiveFrom <= dateOfService
                        && (e.EffectiveTo == null || e.EffectiveTo >= dateOfService))
            .ToListAsync(ct);

        return candidates
            .GroupBy(e => e.ProcedureCode)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.EffectiveFrom).First().BaseAmount);
    }
}
