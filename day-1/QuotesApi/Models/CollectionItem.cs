namespace QuotesApi.Models;

public class CollectionItem
{
    public int QuoteId { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    private CollectionItem() { }

    public CollectionItem(int quoteId, DateTimeOffset addedAt)
    {
        QuoteId = quoteId;
        AddedAt = addedAt;
    }
}