using QuotesApi.Models;

namespace QuotesApi.Features.Collections.Commands;

// The write-side outcome, not a read model: it echoes back what was just
// created/rejected, it does not join or shape data for display.
public sealed class CreateCollectionResult
{
    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors = new Dictionary<string, string[]>();

    public bool Succeeded { get; }
    public int Id { get; }
    public string Name { get; }
    public int OwnerId { get; }
    public string? OwnerUserId { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    private CreateCollectionResult(bool succeeded, int id, string name, int ownerId, string? ownerUserId, IReadOnlyDictionary<string, string[]> errors)
    {
        Succeeded = succeeded;
        Id = id;
        Name = name;
        OwnerId = ownerId;
        OwnerUserId = ownerUserId;
        Errors = errors;
    }

    public static CreateCollectionResult Success(Collection collection) =>
        new(true, collection.Id, collection.Name, collection.OwnerId, collection.OwnerUserId, EmptyErrors);

    public static CreateCollectionResult Failure(IReadOnlyDictionary<string, string[]> errors) =>
        new(false, 0, string.Empty, 0, null, errors);
}
