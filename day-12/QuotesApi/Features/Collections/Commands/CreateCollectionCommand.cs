namespace QuotesApi.Features.Collections.Commands;

public record CreateCollectionCommand(string Name, string? OwnerUserId);
