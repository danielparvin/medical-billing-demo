using Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Billing.Data.Configurations;

internal sealed class LineItemConfiguration : IEntityTypeConfiguration<LineItem>
{
    public void Configure(EntityTypeBuilder<LineItem> builder)
    {
        var modifiers = new ValueConverter<List<string>, string>(
            v => string.Join(',', v),
            v => v.Length == 0
                ? new List<string>()
                : v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

        var comparer = new ValueComparer<List<string>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        builder.Property(l => l.ProcedureCode).HasMaxLength(5).IsRequired();
        builder.Property(l => l.Modifiers).HasConversion(modifiers, comparer).HasMaxLength(50);
        builder.Property(l => l.BilledAmount).HasPrecision(18, 2);
        builder.Property(l => l.AllowedAmount).HasPrecision(18, 2);
        builder.Property(l => l.PricingStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.UnpricedReason).HasMaxLength(100);
        builder.HasIndex(l => new { l.BillId, l.LineNumber }).IsUnique();
        builder.HasMany(l => l.Adjustments).WithOne().HasForeignKey(a => a.LineItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
