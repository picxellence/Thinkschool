using Testcontainers.MsSql;

namespace Quotes.Tests.Integration;

// One SQL Server container for the whole AuthorizationPolicyTests class. Started
// once (~20-40s to become healthy) and disposed once, instead of per test.
public class SqlServerContainerFixture : IAsyncLifetime
{
    private MsSqlContainer _container = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder().Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "SqlServer collection";
}
