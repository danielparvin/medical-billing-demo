using Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Billing.Data;

public class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<LineItem> LineItems => Set<LineItem>();
    public DbSet<LineItemAdjustment> LineItemAdjustments => Set<LineItemAdjustment>();
    public DbSet<FeeScheduleEntry> FeeSchedule => Set<FeeScheduleEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
}
