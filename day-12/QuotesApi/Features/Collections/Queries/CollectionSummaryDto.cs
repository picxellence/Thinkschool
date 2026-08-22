namespace QuotesApi.Features.Collections.Queries;

// Denormalized on purpose: shaped for display, not for round-tripping back
// into the Collection domain entity.
public record CollectionSummaryDto(
    int Id,
    string Name,
    string? Owner,
    int ItemCount,
    List<string> QuoteTexts);
