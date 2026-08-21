namespace QuotesApi.Clients;

public record RandomQuote(string Author, string Text);

public interface IRandomQuoteClient
{
    Task<RandomQuote> GetRandomQuoteAsync(CancellationToken ct);
}
