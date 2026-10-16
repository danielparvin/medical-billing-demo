using Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Billing.Data.Configurations;

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        // SQL Server returns DateTimeKind.Unspecified; mark everything we read as UTC.
        var utc = new ValueConverter<DateTime?, DateTime?>(
            v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        builder.Property(b => b.PatientRef).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.ProcessedAtUtc).HasColumnType("datetime2").HasConversion(utc);
        builder.Property(b => b.TotalAllowed).HasPrecision(18, 2);
        builder.HasMany(b => b.Lines).WithOne().HasForeignKey(l => l.BillId).OnDelete(DeleteBehavior.Cascade);
    }
}
