using System.Net;
using System.Net.Http.Json;
using Billing.Contracts;

namespace Billing.Web.Services;

public sealed class BillingApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<BillSummaryDto>> ListAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<BillSummaryDto>>("api/bills", ct) ?? [];

    public async Task<BillDto?> GetAsync(int id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/bills/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BillDto>(ct);
    }

    public Task<ApiResult<BillDto>> CreateAsync(BillRequest request, CancellationToken ct = default) =>
        SendAsync(() => http.PostAsJsonAsync("api/bills", request, ct), ct);

    public Task<ApiResult<BillDto>> UpdateAsync(int id, BillRequest request, CancellationToken ct = default) =>
        SendAsync(() => http.PutAsJsonAsync($"api/bills/{id}", request, ct), ct);

    public Task<ApiResult<BillDto>> ProcessAsync(int id, CancellationToken ct = default) =>
        SendAsync(() => http.PostAsync($"api/bills/{id}/process", content: null, ct), ct);

    private static async Task<ApiResult<BillDto>> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        try
        {
            using var response = await send();
            if (response.IsSuccessStatusCode)
            {
                var bill = await response.Content.ReadFromJsonAsync<BillDto>(ct);
                return ApiResult<BillDto>.Success(bill!);
            }

            return ApiResult<BillDto>.Failure(await ProblemMessage.FromAsync(response, ct));
        }
        catch (HttpRequestException)
        {
            return ApiResult<BillDto>.Failure("The billing API is unreachable. Is it running?");
        }
    }
}
