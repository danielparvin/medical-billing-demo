using Billing.Data;
using Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Billing.Integration.Tests;

/// <summary>One SQL Server container per test run, shared by every integration test class.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public BillingDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<BillingDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task ResetAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM LineItemAdjustments; DELETE FROM LineItems; DELETE FROM Bills; DELETE FROM FeeSchedule;");
    }

    public async Task AddFeeAsync(string code, decimal baseAmount, DateOnly from, DateOnly? to = null)
    {
        await using var db = CreateDbContext();
        db.FeeSchedule.Add(new FeeScheduleEntry
        {
            ProcedureCode = code,
            Description = $"Test {code}",
            BaseAmount = baseAmount,
            EffectiveFrom = from,
            EffectiveTo = to
        });
        await db.SaveChangesAsync();
    }
}
