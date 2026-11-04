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
    public void OnParametersSetAsync_renders_one_row_per_line_when_the_bill_exists()
    {
        _api.On(HttpMethod.Get, "/api/bills/1", () =>
            FakeApiHandler.Json(HttpStatusCode.OK, Bill("Draft", null, Line(1), Line(2))));

        var cut = RenderExisting();

        Assert.Equal(2, cut.FindAll("[data-testid=line-row]").Count);
    }

    [Fact]
    public void AddLine_appends_a_row()
    {
        var cut = RenderComponent<BillEditor>();
        Assert.Single(cut.FindAll("[data-testid=line-row]"));

        cut.Find("#add-line").Click();

        Assert.Equal(2, cut.FindAll("[data-testid=line-row]").Count);
    }

    [Fact]
    public void RemoveLine_drops_the_row()
    {
        _api.On(HttpMethod.Get, "/api/bills/1", () =>
            FakeApiHandler.Json(HttpStatusCode.OK, Bill("Draft", null, Line(1), Line(2))));
        var cut = RenderExisting();

        cut.FindAll(".remove-line")[0].Click();

        Assert.Single(cut.FindAll("[data-testid=line-row]"));
    }

    [Fact]
    public void ProcessAsync_saves_then_processes_the_bill_and_shows_the_allowed_amounts()
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
    public void SaveAsync_shows_the_validation_error_when_the_api_rejects_the_bill()
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
    public void SaveAsync_shows_a_readable_error_when_the_api_is_unreachable()
    {
        Services.AddSingleton(
            new BillingApiClient(new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://api.test/") }));

        var cut = RenderComponent<BillEditor>();
        cut.Find(".code").Change("A1001");
        cut.Find("#save").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("unreachable", cut.Find("[data-testid=error]").TextContent, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildRenderTree_limits_the_code_field_to_five_characters()
    {
        var cut = RenderComponent<BillEditor>();

        Assert.Equal("5", cut.Find(".code").GetAttribute("maxlength"));
    }

    [Theory]
    [InlineData("#save", "B12345")]
    [InlineData("#save", "A100")]
    [InlineData("#save", "")]
    [InlineData("#save", "A-100")]
    [InlineData("#save", "A 100")]
    [InlineData("#save", "Ä1001")]
    [InlineData("#process", "B12345")]
    [InlineData("#process", "A-100")]
    public void SaveCoreAsync_blocks_the_api_call_and_shows_an_error_when_the_code_is_invalid(string button, string code)
    {
        var cut = RenderComponent<BillEditor>();
        cut.Find(".code").Change(code);

        cut.Find(button).Click();

        Assert.Empty(_api.Calls);
        Assert.Contains("is-invalid", cut.Find(".code").ClassList);
        Assert.Contains("5 letters or digits", cut.Find("[data-testid=code-error]").TextContent);
        Assert.Contains("5 letters or digits", cut.Find("[data-testid=error]").TextContent);
    }

    [Theory]
    [InlineData("A1001")]
    [InlineData("b1234")]
    [InlineData("12345")]
    [InlineData("ABCDE")]
    public void SaveCoreAsync_sends_the_code_to_the_api_when_the_code_is_valid(string code)
    {
        _api.On(HttpMethod.Post, "/api/bills", () =>
            FakeApiHandler.Json(HttpStatusCode.Created, Bill("Draft", null, Line(1))));
        var cut = RenderComponent<BillEditor>();
        cut.Find(".code").Change(code);

        cut.Find("#save").Click();

        cut.WaitForAssertion(() => Assert.Equal(["POST /api/bills"], _api.Calls));
        Assert.Empty(cut.FindAll("[data-testid=code-error]"));
    }

    [Fact]
    public void ShowCodeError_flags_the_field_when_a_bad_code_is_entered_but_not_when_it_is_untouched_and_empty()
    {
        var cut = RenderComponent<BillEditor>();
        Assert.DoesNotContain("is-invalid", cut.Find(".code").ClassList);

        cut.Find(".code").Change("A100");

        Assert.Contains("is-invalid", cut.Find(".code").ClassList);
    }

    [Fact]
    public void ShowCodeError_clears_the_error_when_the_code_is_corrected()
    {
        var cut = RenderComponent<BillEditor>();
        cut.Find(".code").Change("A100");
        cut.Find("#save").Click();

        cut.Find(".code").Change("A1001");

        Assert.DoesNotContain("is-invalid", cut.Find(".code").ClassList);
        Assert.Empty(cut.FindAll("[data-testid=code-error]"));
    }

    [Fact]
    public void ShowCodeError_flags_only_the_bad_line_when_other_lines_have_valid_codes()
    {
        _api.On(HttpMethod.Get, "/api/bills/1", () =>
            FakeApiHandler.Json(HttpStatusCode.OK, Bill("Draft", null, Line(1), Line(2))));
        var cut = RenderExisting();
        cut.FindAll(".code")[1].Change("A100");

        cut.Find("#save").Click();

        Assert.Equal(["GET /api/bills/1"], _api.Calls);
        Assert.DoesNotContain("is-invalid", cut.FindAll(".code")[0].ClassList);
        Assert.Contains("is-invalid", cut.FindAll(".code")[1].ClassList);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}
