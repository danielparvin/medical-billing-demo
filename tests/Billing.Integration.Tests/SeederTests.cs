using Billing.Data;
using Microsoft.EntityFrameworkCore;

namespace Billing.Integration.Tests;

[Collection(DatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SeederTests(SqlServerFixture database) : IAsyncLifetime
{
    private const int SeededEntryCount = 21;

    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seeding_IsIdempotent_AndLoadsAnEffectiveDatedPair()
    {
        await using var db = database.CreateDbContext();
        var seeder = new FeeScheduleSeeder(db);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(SeededEntryCount, await db.FeeSchedule.CountAsync());
        var versions = await db.FeeSchedule.Where(e => e.ProcedureCode == "A1002")
            .OrderBy(e => e.EffectiveFrom).ToListAsync();
        Assert.Equal([115.00m, 120.00m], versions.Select(v => v.BaseAmount));
        Assert.Null(versions[1].EffectiveTo);
    }

    [Fact]
    public async Task Seeding_AnExistingSchedule_AddsOnlyTheMissingEntries()
    {
        await database.AddFeeAsync("A1001", 99.99m, new DateOnly(2025, 1, 1));
        await using var db = database.CreateDbContext();

        await new FeeScheduleSeeder(db).SeedAsync();

        Assert.Equal(SeededEntryCount, await db.FeeSchedule.CountAsync());
        var existing = await db.FeeSchedule.SingleAsync(e => e.ProcedureCode == "A1001");
        Assert.Equal(99.99m, existing.BaseAmount);
        string[] newCodes = ["B1234", "B1235", "B1236", "B1237", "C1234", "C1235", "C1236", "C1237"];
        var seededCodes = await db.FeeSchedule.Select(e => e.ProcedureCode).Distinct().ToListAsync();
        Assert.All(newCodes, code => Assert.Contains(code, seededCodes));
    }
}
