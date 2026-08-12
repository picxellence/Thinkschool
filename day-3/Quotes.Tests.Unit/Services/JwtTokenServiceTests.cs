using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using QuotesApi.Models;
using QuotesApi.Services;

namespace Quotes.Tests.Unit.Services;

public class JwtTokenServiceTests
{
    [Fact]
    public void GenerateAccessToken_ValidUser_ContainsSubClaimWithUserId()
    {
        // Arrange
        var config = Substitute.For<IConfiguration>();
        config["Jwt:Key"].Returns("unit-test-signing-key-at-least-32-bytes-long!");
        config["Jwt:Issuer"].Returns("QuotesApi.UnitTests");
        config["Jwt:Audience"].Returns("QuotesApi.UnitTests.Clients");
        config["Jwt:AccessTokenMinutes"].Returns("15");
        var service = new JwtTokenService(config);
        var user = User.Create("test@example.com", "Password123!");

        // Act
        var token = service.GenerateAccessToken(user);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().ContainSingle(c => c.Type == "sub" && c.Value == user.Id.ToString());
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_ContainsEmailClaim()
    {
        // Arrange
        var config = Substitute.For<IConfiguration>();
        config["Jwt:Key"].Returns("unit-test-signing-key-at-least-32-bytes-long!");
        config["Jwt:Issuer"].Returns("QuotesApi.UnitTests");
        config["Jwt:Audience"].Returns("QuotesApi.UnitTests.Clients");
        config["Jwt:AccessTokenMinutes"].Returns("15");
        var service = new JwtTokenService(config);
        var user = User.Create("test@example.com", "Password123!");

        // Act
        var token = service.GenerateAccessToken(user);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().ContainSingle(c => c.Type == "email" && c.Value == "test@example.com");
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_ContainsThreeScopeClaims()
    {
        // Arrange
        var config = Substitute.For<IConfiguration>();
        config["Jwt:Key"].Returns("unit-test-signing-key-at-least-32-bytes-long!");
        config["Jwt:Issuer"].Returns("QuotesApi.UnitTests");
        config["Jwt:Audience"].Returns("QuotesApi.UnitTests.Clients");
        config["Jwt:AccessTokenMinutes"].Returns("15");
        var service = new JwtTokenService(config);
        var user = User.Create("test@example.com", "Password123!");

        // Act
        var token = service.GenerateAccessToken(user);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Where(c => c.Type == "scope").Select(c => c.Value)
            .Should().BeEquivalentTo(new[] { "quotes.read", "quotes.write", "quotes.delete" });
    }

    [Fact]
    public void GenerateAccessToken_ConfiguredIssuerAndAudience_AreSetOnToken()
    {
        // Arrange
        var config = Substitute.For<IConfiguration>();
        config["Jwt:Key"].Returns("unit-test-signing-key-at-least-32-bytes-long!");
        config["Jwt:Issuer"].Returns("QuotesApi.UnitTests");
        config["Jwt:Audience"].Returns("QuotesApi.UnitTests.Clients");
        config["Jwt:AccessTokenMinutes"].Returns("15");
        var service = new JwtTokenService(config);
        var user = User.Create("test@example.com", "Password123!");

        // Act
        var token = service.GenerateAccessToken(user);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be("QuotesApi.UnitTests");
        jwt.Audiences.Should().Contain("QuotesApi.UnitTests.Clients");
    }

    [Fact]
    public void AccessTokenMinutes_ConfiguredValue_ReturnsParsedInt()
    {
        // Arrange
        var config = Substitute.For<IConfiguration>();
        config["Jwt:AccessTokenMinutes"].Returns("15");
        var service = new JwtTokenService(config);

        // Act
        var result = service.AccessTokenMinutes;

        // Assert
        result.Should().Be(15);
    }
}
