using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuotesApi.Clients;

// zenquotes.io/api/random - chosen over api.quotable.io/random because the latter
// was unreachable (connection failure) when checked; zenquotes responded 200.
public class RandomQuoteClient : IRandomQuoteClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public RandomQuoteClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RandomQuote> GetRandomQuoteAsync(CancellationToken ct)
    {
        var response = await _httpClient.GetAsync("api/random", ct);
        response.EnsureSuccessStatusCode();

        var quotes = await response.Content.ReadFromJsonAsync<List<ZenQuoteDto>>(JsonOptions, ct);
        var quote = quotes?.FirstOrDefault()
            ?? throw new InvalidOperationException("Upstream returned no quote.");

        return new RandomQuote(quote.A, quote.Q);
    }

    // Field names match the upstream API's response shape exactly (single-letter
    // JSON keys), not a naming choice of ours.
    private sealed record ZenQuoteDto(
        [property: JsonPropertyName("q")] string Q,
        [property: JsonPropertyName("a")] string A);
}
