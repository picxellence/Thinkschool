using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace QuotesApi.Tests;

// Program.cs reads Jwt:*/Entra:* config into local variables (and AddAuthentication
// closures) before builder.Build() runs, so a WebApplicationFactory ConfigureAppConfiguration
// override — which only takes effect once the host is built — arrives too late to influence
// those reads. Environment variables are picked up by WebApplication.CreateBuilder itself, so
// setting them (then restoring immediately after the host is forced to build) is what actually
// reaches those early reads without leaking into other tests' processes for longer than necessary.
public class AuthorizationPolicyTestsFactory : WebApplicationFactory<Program>
{
    public const string JwtKey = "authz-policy-tests-signing-key-do-not-use-in-prod!";
    public const string JwtIssuer = "QuotesApi.Tests";
    public const string JwtAudience = "QuotesApi.Tests.Clients";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"quotesapi-authz-tests-{Guid.NewGuid():N}.db");

    public AuthorizationPolicyTestsFactory()
    {
        var overrides = new Dictionary<string, string?>
        {
            ["Jwt__Key"] = JwtKey,
            ["Jwt__Issuer"] = JwtIssuer,
            ["Jwt__Audience"] = JwtAudience,
            ["Jwt__AccessTokenMinutes"] = "15",
            ["Jwt__RefreshTokenDays"] = "7",
            ["Entra__TenantId"] = "00000000-0000-0000-0000-000000000000",
            ["Entra__Audience"] = "00000000-0000-0000-0000-000000000001",
            ["ConnectionStrings__Default"] = $"Data Source={_dbPath}"
        };

        var originalValues = new Dictionary<string, string?>();
        foreach (var (key, value) in overrides)
        {
            originalValues[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        try
        {
            _ = Server;
        }
        finally
        {
            foreach (var (key, original) in originalValues)
                Environment.SetEnvironmentVariable(key, original);
        }
    }

    public string MintToken(string sub, params string[] scopes)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, sub) };
        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}

public class AuthorizationPolicyTests : IClassFixture<AuthorizationPolicyTestsFactory>
{
    private readonly AuthorizationPolicyTestsFactory _factory;

    public AuthorizationPolicyTests(AuthorizationPolicyTestsFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(string? token = null)
    {
        var client = _factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<QuoteDto> CreateQuoteAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/quotes", new { author = "Author", text = "Text" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QuoteDto>())!;
    }

    [Fact]
    public async Task Post_WithWriteScope_Returns201()
    {
        var client = CreateClient(_factory.MintToken(Guid.NewGuid().ToString(), "quotes.write"));

        var response = await client.PostAsJsonAsync("/api/quotes", new { author = "A", text = "T" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithOnlyReadScope_Returns403()
    {
        var client = CreateClient(_factory.MintToken(Guid.NewGuid().ToString(), "quotes.read"));

        var response = await client.PostAsJsonAsync("/api/quotes", new { author = "A", text = "T" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithNoToken_Returns401()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/quotes", new { author = "A", text = "T" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ByCreator_Returns204()
    {
        var creator = Guid.NewGuid().ToString();
        var client = CreateClient(_factory.MintToken(creator, "quotes.write", "quotes.delete"));

        var quote = await CreateQuoteAsync(client);
        var response = await client.DeleteAsync($"/api/quotes/{quote.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ByDifferentUserWithDeleteScope_Returns403()
    {
        var creatorClient = CreateClient(_factory.MintToken(Guid.NewGuid().ToString(), "quotes.write"));
        var quote = await CreateQuoteAsync(creatorClient);

        var otherClient = CreateClient(_factory.MintToken(Guid.NewGuid().ToString(), "quotes.delete"));
        var response = await otherClient.DeleteAsync($"/api/quotes/{quote.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ByCreatorWithoutDeleteScope_Returns403()
    {
        var creator = Guid.NewGuid().ToString();
        var client = CreateClient(_factory.MintToken(creator, "quotes.write"));

        var quote = await CreateQuoteAsync(client);
        var response = await client.DeleteAsync($"/api/quotes/{quote.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private record QuoteDto(int Id, string Author, string Text, string? CreatedByUserId);
}
