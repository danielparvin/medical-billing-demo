using Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Data.Configurations;

internal sealed class FeeScheduleEntryConfiguration : IEntityTypeConfiguration<FeeScheduleEntry>
{
    public void Configure(EntityTypeBuilder<FeeScheduleEntry> builder)
    {
        builder.ToTable("FeeSchedule");
        builder.Property(e => e.ProcedureCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.BaseAmount).HasPrecision(18, 2);
        builder.HasIndex(e => new { e.ProcedureCode, e.EffectiveFrom }).IsUnique();
    }
}
