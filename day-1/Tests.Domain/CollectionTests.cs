using FluentAssertions;
using QuotesApi.Models;
using Xunit;

namespace Tests.Domain;

public class CollectionTests
{
    [Fact]
    public void Constructor_WithEmptyName_Throws()
    {
        Action act = () => new Collection("", ownerId: 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNameOver80Chars_Throws()
    {
        var longName = new string('a', 81);
        Action act = () => new Collection(longName, ownerId: 1); 
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddItem_51stItem_Throws()
    {
        var collection = new Collection("Test", ownerId: 1);
        for (int i = 1; i <= 50; i++)
            collection.AddItem(i, DateTimeOffset.UtcNow);

        Action act = () => collection.AddItem(51, DateTimeOffset.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddItem_DuplicateQuoteId_Throws()
    {
        var collection = new Collection("Test", ownerId: 1);
        collection.AddItem(1, DateTimeOffset.UtcNow);

        Action act = () => collection.AddItem(1, DateTimeOffset.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveItem_NonExistentItem_Throws()
    {
        var collection = new Collection("Test", ownerId: 1);
        collection.AddItem(1, DateTimeOffset.UtcNow);
        Action act = () => collection.RemoveItem(999);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddThenRemove_LeavesZeroItems()
    {
        var collection = new Collection("Test", ownerId: 1);
        collection.AddItem(1, DateTimeOffset.UtcNow);

        collection.RemoveItem(1);

        collection.Items.Should().BeEmpty();
    }
}
