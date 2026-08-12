using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QuotesApi.Data;

namespace QuotesApi.Migrations.SqlServer;

// Used only by `dotnet ef migrations add` to scaffold this project's migrations.
// Bypasses QuotesApi's own Program.cs startup (and its migrate/seed block) so
// scaffolding never needs a live connection. The connection string here is
// never actually opened.
public class SqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<QuotesDbContext>
{
    public QuotesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<QuotesDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=QuotesApi.DesignTime;User Id=sa;Password=DesignTime-Only-1!;TrustServerCertificate=True;",
            sql => sql.MigrationsAssembly("QuotesApi.Migrations.SqlServer"));

        return new QuotesDbContext(optionsBuilder.Options);
    }
}
