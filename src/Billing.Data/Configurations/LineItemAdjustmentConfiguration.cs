using Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Data.Configurations;

internal sealed class LineItemAdjustmentConfiguration : IEntityTypeConfiguration<LineItemAdjustment>
{
    public void Configure(EntityTypeBuilder<LineItemAdjustment> builder)
    {
        builder.Property(a => a.Step).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Factor).HasPrecision(18, 6);
        builder.Property(a => a.ResultingAmount).HasPrecision(18, 6);
    }
}
