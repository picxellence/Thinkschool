namespace QuotesApi.Models;

public class CollectionItem
{
    public int QuoteId { get; private set; }
    public DateTime AddedAt { get; private set; }

    private CollectionItem() { }

    public CollectionItem(int quoteId)
    {
        QuoteId = quoteId;
        AddedAt = DateTime.UtcNow;
    }
}