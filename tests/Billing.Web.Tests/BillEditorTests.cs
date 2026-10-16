using System.Net;
using Billing.Contracts;
using Billing.Web.Components.Pages;
using Billing.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Billing.Web.Tests;

[Trait("Category", "Unit")]
public sealed class BillEditorTests : TestContext
{
    private readonly FakeApiHandler _api = new();

    public BillEditorTests()
    {
        Services.AddSingleton(new BillingApiClient(new HttpClient(_api) { BaseAddress = new Uri("http://api.test/") }));
    }

    private static BillDto Bill(string status, decimal? allowed, params LineItemDto[] lines) => new(
        1, "TEST-0001", new DateOnly(2026, 3, 1), status,
        status == "Processed" ? new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc) : null,
        allowed, lines);

    private static LineItemDto Line(int number, decimal? allowed = null, string? pricingStatus = null) =>
        new(number, "A1001", [], 2, 500.00m, allowed, pricingStatus, null, []);

    private IRenderedComponent<BillEditor> RenderExisting()
    {
        var cut = RenderComponent<BillEditor>(p => p.Add(c => c.Id, 1));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=line-row]")));
        return cut;
    }

    [Fact]
    public void ExistingBill_RendersOneRowPerLine()
    {
        _api.On(HttpMethod.Get, "/api/bills/1", () =>
            FakeApiHandler.Json(HttpStatusCode.OK, Bill("Draft", null, Line(1), Line(2))));

        var cut = RenderExisting();

        Assert.Equal(2, cut.FindAll("[data-testid=line-row]").Count);
    }

    [Fact]
    public void AddLine_AppendsARow()
    {
        var cut = RenderComponent<BillEditor>();
        Assert.Single(cut.FindAll("[data-testid=line-row]"));

        cut.Find("#add-line").Click();

        Assert.Equal(2, cut.FindAll("[data-testid=line-row]").Count);
    }

    [Fact]
    public void RemoveLine_DropsTheRow()
    {
        _api.On(HttpMethod.Get, "/api/bills/1", () =>
            FakeApiHandler.Json(HttpStatusCode.OK, Bill("Draft", null, Line(1), Line(2))));
        var cut = RenderExisting();

        cut.FindAll(".remove-line")[0].Click();

        Assert.Single(cut.FindAll("[data-testid=line-row]"));
    }

    [Fact]
    public void ProcessBill_SavesThenProcessesAndShowsAllowedAmounts()
    {
        _api.On(HttpMethod.Get, "/api/bills/1", () =>
            FakeApiHandler.Json(HttpStatusCode.OK, Bill("Draft", null, Line(1))));
        _api.On(HttpMethod.Put, "/api/bills/1", () =>
            FakeApiHandler.Json(HttpStatusCode.OK, Bill("Draft", null, Line(1))));
        _api.On(HttpMethod.Post, "/api/bills/1/process", () =>
            FakeApiHandler.Json(HttpStatusCode.OK, Bill("Processed", 200.00m, Line(1, 200.00m, "Priced"))));
        var cut = RenderExisting();

        cut.Find("#process").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("200.00", cut.Find("[data-testid=allowed]").TextContent));
        Assert.Equal(["GET /api/bills/1", "PUT /api/bills/1", "POST /api/bills/1/process"], _api.Calls);
    }

    [Fact]
    public void ApiValidationError_IsShownToTheUser()
    {
        _api.On(HttpMethod.Get, "/api/bills/1", () =>
            FakeApiHandler.Json(HttpStatusCode.OK, Bill("Draft", null, Line(1))));
        _api.On(HttpMethod.Put, "/api/bills/1", () => FakeApiHandler.Problem(
            HttpStatusCode.BadRequest,
            """{"title":"One or more validation errors occurred.","status":400,"errors":{"Lines":["Line number 1 is used more than once."]}}"""));
        var cut = RenderExisting();

        cut.Find("#save").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Line number 1 is used more than once.", cut.Find("[data-testid=error]").TextContent));
    }

    [Fact]
    public void UnreachableApi_ShowsAReadableError()
    {
        Services.AddSingleton(
            new BillingApiClient(new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://api.test/") }));

        var cut = RenderComponent<BillEditor>();
        cut.Find("#save").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("unreachable", cut.Find("[data-testid=error]").TextContent, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}
