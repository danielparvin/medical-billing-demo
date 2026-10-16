using System.Net;
using System.Net.Http.Json;

namespace Billing.Web.Tests;

/// <summary>Stands in for the Billing API: programmed responses keyed by "METHOD /path", with a call log.</summary>
internal sealed class FakeApiHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = [];

    public List<string> Calls { get; } = [];

    public void On(HttpMethod method, string path, Func<HttpResponseMessage> respond) =>
        _routes[$"{method} {path}"] = respond;

    public static HttpResponseMessage Json<T>(HttpStatusCode status, T body) =>
        new(status) { Content = JsonContent.Create(body) };

    public static HttpResponseMessage Problem(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/problem+json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        Calls.Add(key);
        return Task.FromResult(_routes.TryGetValue(key, out var respond)
            ? respond()
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
