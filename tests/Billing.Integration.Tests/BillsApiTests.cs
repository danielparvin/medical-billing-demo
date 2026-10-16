using System.Net;
using System.Net.Http.Json;
using Billing.Contracts;

namespace Billing.Integration.Tests;

[Collection(DatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class BillsApiTests(SqlServerFixture database) : IAsyncLifetime
{
    private static readonly DateOnly EffectiveFrom = new(2026, 1, 1);

    private BillingApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        _factory = new BillingApiFactory(database.ConnectionString);
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<BillDto> CreateAsync(BillRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/bills", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BillDto>())!;
    }

    private async Task<BillDto> ProcessAsync(int id)
    {
        var response = await _client.PostAsync($"/api/bills/{id}/process", content: null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BillDto>())!;
    }

    [Fact]
    public async Task CreateThenProcessThenGet_ReturnsAllowedAmountsAndTotal()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);
        var created = await CreateAsync(TestBills.Request(TestBills.Line(1, "A1001", billed: 500m, units: 2)));

        var processed = await ProcessAsync(created.Id);
        var fetched = await _client.GetFromJsonAsync<BillDto>($"/api/bills/{created.Id}");

        Assert.Equal("Processed", processed.Status);
        Assert.Equal(200.00m, processed.TotalAllowed);
        Assert.Equal(200.00m, processed.Lines[0].AllowedAmount);
        Assert.Equal("Priced", processed.Lines[0].PricingStatus);
        Assert.Equal(200.00m, fetched!.TotalAllowed);
        Assert.NotEmpty(fetched.Lines[0].Adjustments);
    }

    [Fact]
    public async Task ProcessingAMultiLineBill_AppliesTheMultipleProcedureDiscount()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);
        await database.AddFeeAsync("B2001", 200.00m, EffectiveFrom);
        var created = await CreateAsync(TestBills.Request(
            TestBills.Line(1, "A1001", billed: 1000m),
            TestBills.Line(2, "B2001", billed: 1000m)));

        var processed = await ProcessAsync(created.Id);

        Assert.Equal(50.00m, processed.Lines[0].AllowedAmount);
        Assert.Equal(200.00m, processed.Lines[1].AllowedAmount);
        Assert.Equal(250.00m, processed.TotalAllowed);
        Assert.Contains(processed.Lines[0].Adjustments, a => a.Step == "MultipleProcedureDiscount");
    }

    [Fact]
    public async Task ProcessedAtUtc_EqualsTheInjectedClock()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);
        var created = await CreateAsync(TestBills.Request(TestBills.Line(1, "A1001", 500m)));

        var processed = await ProcessAsync(created.Id);

        Assert.Equal(new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc), processed.ProcessedAtUtc);
    }

    [Fact]
    public async Task EditingAProcessedBill_ReturnsItToDraftAndClearsResults()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);
        var created = await CreateAsync(TestBills.Request(TestBills.Line(1, "A1001", 500m)));
        await ProcessAsync(created.Id);

        var edit = TestBills.Request(TestBills.Line(1, "A1001", 500m, units: 3));
        var response = await _client.PutAsJsonAsync($"/api/bills/{created.Id}", edit);
        var updated = await response.Content.ReadFromJsonAsync<BillDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Draft", updated!.Status);
        Assert.Null(updated.TotalAllowed);
        Assert.Null(updated.ProcessedAtUtc);
        Assert.Null(updated.Lines[0].AllowedAmount);
        Assert.Empty(updated.Lines[0].Adjustments);
        Assert.Equal(3, updated.Lines[0].Units);
    }

    [Fact]
    public async Task Reprocessing_ProducesIdenticalResults()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);
        var created = await CreateAsync(TestBills.Request(TestBills.Line(1, "A1001", 500m, 1, "50")));

        var first = await ProcessAsync(created.Id);
        var second = await ProcessAsync(created.Id);

        Assert.Equal(first.TotalAllowed, second.TotalAllowed);
        Assert.Equal(first.Lines[0].AllowedAmount, second.Lines[0].AllowedAmount);
        Assert.Equal(first.Lines[0].Adjustments.Count, second.Lines[0].Adjustments.Count);
    }

    [Fact]
    public async Task LineWithoutAFeeScheduleEntry_IsFlaggedButTheBillStillProcesses()
    {
        var created = await CreateAsync(TestBills.Request(TestBills.Line(1, "Z9999", 100m)));

        var processed = await ProcessAsync(created.Id);

        Assert.Equal("Processed", processed.Status);
        Assert.Equal("Unpriced", processed.Lines[0].PricingStatus);
        Assert.Equal("NoFeeScheduleEntry", processed.Lines[0].UnpricedReason);
        Assert.Equal(0.00m, processed.Lines[0].AllowedAmount);
        Assert.Equal(0.00m, processed.TotalAllowed);
    }

    [Fact]
    public async Task LowercaseProcedureCode_IsMatchedToTheFeeSchedule()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);
        var created = await CreateAsync(TestBills.Request(TestBills.Line(1, "a1001", 500m)));

        var processed = await ProcessAsync(created.Id);

        Assert.Equal("A1001", processed.Lines[0].ProcedureCode);
        Assert.Equal("Priced", processed.Lines[0].PricingStatus);
    }

    [Fact]
    public async Task BillWithNoLines_Returns400ProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/bills", TestBills.Request());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownModifier_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/bills",
            TestBills.Request(TestBills.Line(1, "A1001", 100m, 1, "99")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateModifierOnALine_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/bills",
            TestBills.Request(TestBills.Line(1, "A1001", 100m, 1, "50", "50")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateLineNumbers_Return400()
    {
        var response = await _client.PostAsJsonAsync("/api/bills",
            TestBills.Request(TestBills.Line(1, "A1001", 100m), TestBills.Line(1, "B2001", 100m)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AbsurdUnits_Return400_InsteadOfOverflowingTheDatabase()
    {
        var response = await _client.PostAsJsonAsync("/api/bills",
            TestBills.Request(TestBills.Line(1, "A1001", 100m, units: 1000)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownBill_Returns404()
    {
        var get = await _client.GetAsync("/api/bills/999999");
        var process = await _client.PostAsync("/api/bills/999999/process", content: null);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, process.StatusCode);
    }

    [Fact]
    public async Task FeeLookup_ReturnsTheEntryInEffect_404ForUnknown_400WithoutADate()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);

        var found = await _client.GetAsync("/api/fee-schedule/A1001?dateOfService=2026-03-01");
        var unknown = await _client.GetAsync("/api/fee-schedule/Z9999?dateOfService=2026-03-01");
        var noDate = await _client.GetAsync("/api/fee-schedule/A1001");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(100.00m, (await found.Content.ReadFromJsonAsync<FeeDto>())!.BaseAmount);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noDate.StatusCode);
    }
}
