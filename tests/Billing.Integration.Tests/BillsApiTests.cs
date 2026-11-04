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
    public async Task Process_returns_and_persists_the_allowed_amounts_and_total_when_the_bill_has_a_priced_line()
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
    public async Task Process_sets_ProcessedAtUtc_to_the_injected_clock_time()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);
        var created = await CreateAsync(TestBills.Request(TestBills.Line(1, "A1001", 500m)));

        var processed = await ProcessAsync(created.Id);

        Assert.Equal(new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc), processed.ProcessedAtUtc);
    }

    [Fact]
    public async Task Update_returns_the_bill_to_draft_and_clears_the_results_when_the_bill_was_processed()
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
    public async Task Process_produces_identical_results_when_the_bill_is_processed_twice()
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
    public async Task Process_flags_the_line_as_unpriced_and_still_processes_the_bill_when_a_line_has_no_fee_schedule_entry()
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
    public async Task Process_matches_the_fee_schedule_entry_when_the_procedure_code_is_lowercase()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);
        var created = await CreateAsync(TestBills.Request(TestBills.Line(1, "a1001", 500m)));

        var processed = await ProcessAsync(created.Id);

        Assert.Equal("A1001", processed.Lines[0].ProcedureCode);
        Assert.Equal("Priced", processed.Lines[0].PricingStatus);
    }

    [Fact]
    public async Task Create_returns_400_problem_details_when_the_bill_has_no_lines()
    {
        var response = await _client.PostAsJsonAsync("/api/bills", TestBills.Request());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_returns_400_when_a_line_has_an_unknown_modifier()
    {
        var response = await _client.PostAsJsonAsync("/api/bills",
            TestBills.Request(TestBills.Line(1, "A1001", 100m, 1, "99")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_400_when_a_line_repeats_a_modifier()
    {
        var response = await _client.PostAsJsonAsync("/api/bills",
            TestBills.Request(TestBills.Line(1, "A1001", 100m, 1, "50", "50")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_400_when_line_numbers_are_duplicated()
    {
        var response = await _client.PostAsJsonAsync("/api/bills",
            TestBills.Request(TestBills.Line(1, "A1001", 100m), TestBills.Line(1, "B2001", 100m)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_400_instead_of_overflowing_the_database_when_the_units_are_too_large()
    {
        var response = await _client.PostAsJsonAsync("/api/bills",
            TestBills.Request(TestBills.Line(1, "A1001", 100m, units: 1000)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_404_when_the_bill_does_not_exist()
    {
        var response = await _client.GetAsync("/api/bills/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Process_returns_404_when_the_bill_does_not_exist()
    {
        var response = await _client.PostAsync("/api/bills/999999/process", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_the_fee_in_effect_when_the_procedure_code_has_an_entry_on_the_date_of_service()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);

        var response = await _client.GetAsync("/api/fee-schedule/A1001?dateOfService=2026-03-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(100.00m, (await response.Content.ReadFromJsonAsync<FeeDto>())!.BaseAmount);
    }

    [Fact]
    public async Task Get_returns_404_when_the_fee_procedure_code_is_unknown()
    {
        var response = await _client.GetAsync("/api/fee-schedule/Z9999?dateOfService=2026-03-01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_400_when_the_fee_lookup_has_no_date_of_service()
    {
        await database.AddFeeAsync("A1001", 100.00m, EffectiveFrom);

        var response = await _client.GetAsync("/api/fee-schedule/A1001");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
