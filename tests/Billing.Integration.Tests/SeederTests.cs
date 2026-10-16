using Billing.Data;
using Microsoft.EntityFrameworkCore;

namespace Billing.Integration.Tests;

[Collection(DatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SeederTests(SqlServerFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seeding_IsIdempotent_AndLoadsAnEffectiveDatedPair()
    {
        await using var db = database.CreateDbContext();
        var seeder = new FeeScheduleSeeder(db);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(9, await db.FeeSchedule.CountAsync());
        var versions = await db.FeeSchedule.Where(e => e.ProcedureCode == "A1002")
            .OrderBy(e => e.EffectiveFrom).ToListAsync();
        Assert.Equal([115.00m, 120.00m], versions.Select(v => v.BaseAmount));
        Assert.Null(versions[1].EffectiveTo);
    }
}
