using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Features.Collections.Commands;
using QuotesApi.Repositories;

namespace Quotes.Tests.Integration;

public class CreateCollectionCommandHandlerTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"quotesapi-createcollection-tests-{Guid.NewGuid():N}.db");
    private readonly QuotesDbContext _context;
    private readonly CreateCollectionCommandHandler _handler;

    public CreateCollectionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<QuotesDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _context = new QuotesDbContext(options);
        _context.Database.Migrate();
        _handler = new CreateCollectionCommandHandler(new CollectionRepository(_context));
    }

    public void Dispose()
    {
        _context.Dispose();

        foreach (var path in new[] { _dbPath, $"{_dbPath}-wal", $"{_dbPath}-shm" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_CreatesCollectionAndReturnsId()
    {
        var result = await _handler.HandleAsync(new CreateCollectionCommand("My Collection", "owner-123"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Id > 0);
        Assert.Equal("My Collection", result.Name);
        Assert.Equal("owner-123", result.OwnerUserId);

        var stored = await _context.Collections.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("My Collection", stored!.Name);
        Assert.Equal("owner-123", stored.OwnerUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public async Task HandleAsync_InvalidName_ReturnsFailureWithoutPersisting(string name)
    {
        var result = await _handler.HandleAsync(new CreateCollectionCommand(name, "owner-123"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("name", result.Errors.Keys);
        Assert.Empty(_context.Collections);
    }

    [Fact]
    public async Task HandleAsync_NameTooLong_ReturnsFailureWithoutPersisting()
    {
        var longName = new string('a', 81);

        var result = await _handler.HandleAsync(new CreateCollectionCommand(longName, "owner-123"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("name", result.Errors.Keys);
        Assert.Empty(_context.Collections);
    }

    [Fact]
    public async Task HandleAsync_NoOwnerUserId_StampsNullOwner()
    {
        var result = await _handler.HandleAsync(new CreateCollectionCommand("Ownerless Collection", null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.OwnerUserId);
    }
}
