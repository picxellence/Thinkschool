using QuotesApi.Models;
using QuotesApi.Repositories;

namespace QuotesApi.Features.Collections.Commands;

public class CreateCollectionCommandHandler
{
    private readonly ICollectionRepository _repository;

    public CreateCollectionCommandHandler(ICollectionRepository repository)
    {
        _repository = repository;
    }

    public async Task<CreateCollectionResult> HandleAsync(CreateCollectionCommand command, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length < 3 || command.Name.Length > 80)
            errors["name"] = new[] { "Name is required and must be between 3 and 80 characters." };

        if (errors.Count > 0)
            return CreateCollectionResult.Failure(errors);

        var collection = new Collection(command.Name, ownerId: 0, ownerUserId: command.OwnerUserId);
        var created = await _repository.AddAsync(collection, ct);

        return CreateCollectionResult.Success(created);
    }
}
