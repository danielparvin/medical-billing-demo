using System.Text.Json;

namespace Billing.Web.Services;

internal static class ProblemMessage
{
    public static async Task<string> FromAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var messages = new List<string>();
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                {
                    foreach (var message in field.Value.EnumerateArray())
                    {
                        messages.Add(message.GetString() ?? "");
                    }
                }
            }

            if (messages.Count > 0)
            {
                return string.Join(" ", messages);
            }

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString()!;
            }
        }
        catch (JsonException)
        {
            // fall through to the generic message
        }

        return $"The request failed ({(int)response.StatusCode}).";
    }
}
