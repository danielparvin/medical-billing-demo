using System.Globalization;
using Billing.Data;

namespace Billing.Integration.Tests;

[Collection(DatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class FeeScheduleRepositoryTests(SqlServerFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static DateOnly D(string iso) => DateOnly.Parse(iso, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("2025-12-31", false)] // day before EffectiveFrom
    [InlineData("2026-01-01", true)]  // exactly on EffectiveFrom
    [InlineData("2026-06-30", true)]  // exactly on EffectiveTo (inclusive)
    [InlineData("2026-07-01", false)] // day after EffectiveTo
    public async Task Find_HonoursTheInclusiveEffectiveDateRange(string dateOfService, bool expectedFound)
    {
        await database.AddFeeAsync("A1001", 100.00m, D("2026-01-01"), D("2026-06-30"));
        await using var db = database.CreateDbContext();

        var entry = await new FeeScheduleRepository(db).FindAsync("A1001", D(dateOfService));

        Assert.Equal(expectedFound, entry is not null);
    }

    [Fact]
    public async Task Find_MatchesAnOpenEndedEntryFarIntoTheFuture()
    {
        await database.AddFeeAsync("A1001", 100.00m, D("2026-01-01"));
        await using var db = database.CreateDbContext();

        var entry = await new FeeScheduleRepository(db).FindAsync("A1001", D("2035-05-05"));

        Assert.NotNull(entry);
    }

    [Fact]
    public async Task Find_PicksTheVersionInEffectOnTheDateOfService()
    {
        await database.AddFeeAsync("A1002", 115.00m, D("2025-01-01"), D("2025-12-31"));
        await database.AddFeeAsync("A1002", 120.00m, D("2026-01-01"));
        await using var db = database.CreateDbContext();
        var repository = new FeeScheduleRepository(db);

        var old = await repository.FindAsync("A1002", D("2025-06-15"));
        var current = await repository.FindAsync("A1002", D("2026-06-15"));

        Assert.Equal(115.00m, old!.BaseAmount);
        Assert.Equal(120.00m, current!.BaseAmount);
    }

    [Fact]
    public async Task Find_ReturnsNullForAnUnknownCode()
    {
        await using var db = database.CreateDbContext();

        var entry = await new FeeScheduleRepository(db).FindAsync("Z9999", D("2026-03-01"));

        Assert.Null(entry);
    }

    [Fact]
    public async Task GetBaseFees_ReturnsOnlyCodesThatHaveAnEntryInEffect()
    {
        await database.AddFeeAsync("A1001", 100.00m, D("2026-01-01"));
        await database.AddFeeAsync("B2001", 250.00m, D("2026-01-01"));
        await using var db = database.CreateDbContext();

        var fees = await new FeeScheduleRepository(db)
            .GetBaseFeesAsync(["A1001", "Z9999"], D("2026-03-01"));

        Assert.Equal(100.00m, Assert.Single(fees).Value);
        Assert.True(fees.ContainsKey("A1001"));
    }
}
