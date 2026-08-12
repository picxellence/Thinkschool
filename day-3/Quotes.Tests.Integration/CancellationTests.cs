using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Quotes.Tests.Integration;

public class CancellationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CancellationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateCollection_WhenCancelled_ThrowsTaskCanceledException()
    {
        var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource();

        var request = new { name = "Cancel Test", ownerId = 1 };

        // Cancel immediately, before the request completes
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.PostAsJsonAsync("/collections", request, cts.Token);
        });
    }
}