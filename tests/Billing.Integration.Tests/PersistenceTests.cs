using Billing.Data;
using Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Billing.Integration.Tests;

[Collection(DatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PersistenceTests(SqlServerFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetAsync_returns_the_saved_values_when_a_bill_has_lines_modifiers_and_adjustments()
    {
        var processedAt = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        int billId;
        await using (var db = database.CreateDbContext())
        {
            var bill = new Bill
            {
                PatientRef = "TEST-0001",
                DateOfService = new DateOnly(2026, 3, 1),
                Status = BillStatus.Processed,
                ProcessedAtUtc = processedAt,
                TotalAllowed = 60.00m,
                Lines =
                [
                    new LineItem
                    {
                        LineNumber = 1,
                        ProcedureCode = "A1001",
                        Modifiers = ["50", "26"],
                        Units = 1,
                        BilledAmount = 100.00m,
                        AllowedAmount = 60.00m,
                        PricingStatus = LinePricingStatus.Priced,
                        Adjustments = [new LineItemAdjustment { Sequence = 1, Step = "BaseFee", Factor = null, ResultingAmount = 100.000000m }]
                    }
                ]
            };
            db.Bills.Add(bill);
            await db.SaveChangesAsync();
            billId = bill.Id;
        }

        await using var readDb = database.CreateDbContext();
        var loaded = await new BillRepository(readDb).GetAsync(billId);

        Assert.NotNull(loaded);
        Assert.Equal(BillStatus.Processed, loaded.Status);
        Assert.Equal(DateTimeKind.Utc, loaded.ProcessedAtUtc!.Value.Kind);
        Assert.Equal(processedAt, loaded.ProcessedAtUtc);
        var line = Assert.Single(loaded.Lines);
        Assert.Equal(["50", "26"], line.Modifiers);
        Assert.Equal(LinePricingStatus.Priced, line.PricingStatus);
        Assert.Equal("BaseFee", Assert.Single(line.Adjustments).Step);
    }

    [Fact]
    public async Task GetAsync_returns_lines_in_line_number_order_when_they_were_saved_out_of_order()
    {
        int billId;
        await using (var db = database.CreateDbContext())
        {
            var bill = new Bill
            {
                PatientRef = "TEST-0002",
                DateOfService = new DateOnly(2026, 3, 1),
                Lines =
                [
                    new LineItem { LineNumber = 2, ProcedureCode = "A1001", Units = 1, BilledAmount = 1m },
                    new LineItem { LineNumber = 1, ProcedureCode = "A1001", Units = 1, BilledAmount = 1m }
                ]
            };
            db.Bills.Add(bill);
            await db.SaveChangesAsync();
            billId = bill.Id;
        }

        await using var readDb = database.CreateDbContext();
        var loaded = await new BillRepository(readDb).GetAsync(billId);

        Assert.Equal([1, 2], loaded!.Lines.Select(l => l.LineNumber));
    }

    [Fact]
    public async Task SaveChangesAsync_throws_a_DbUpdateException_when_two_fee_entries_have_the_same_code_and_effective_from()
    {
        await database.AddFeeAsync("A1001", 100.00m, new DateOnly(2026, 1, 1));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.AddFeeAsync("A1001", 105.00m, new DateOnly(2026, 1, 1)));
    }
}
