using Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Billing.Data;

public interface IBillRepository
{
    Task<Bill?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Bill>> ListAsync(CancellationToken ct = default);
    void Add(Bill bill);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public sealed class BillRepository(BillingDbContext db) : IBillRepository
{
    public Task<Bill?> GetAsync(int id, CancellationToken ct = default) =>
        db.Bills
            .Include(b => b.Lines.OrderBy(l => l.LineNumber))
            .ThenInclude(l => l.Adjustments.OrderBy(a => a.Sequence))
            .SingleOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<Bill>> ListAsync(CancellationToken ct = default) =>
        await db.Bills.AsNoTracking().OrderByDescending(b => b.Id).ToListAsync(ct);

    public void Add(Bill bill) => db.Bills.Add(bill);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
