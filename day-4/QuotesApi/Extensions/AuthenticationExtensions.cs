using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Authorization;

namespace QuotesApi.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration config)
    {
        // Fail fast at startup instead of producing a null reference on first request.
        var jwtKey = config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var jwtIssuer = config["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var jwtAudience = config["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

        var tenantId = config["Entra:TenantId"]
            ?? throw new InvalidOperationException("Entra:TenantId is not configured.");
        var entraAudience = config["Entra:Audience"]
            ?? throw new InvalidOperationException("Entra:Audience is not configured.");

        // v2 issuer once the manifest has requestedAccessTokenVersion = 2.
        var entraV2Issuer = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        // v1 issuer, which is what the az CLI returns if it is still on version 1.
        var entraV1Issuer = $"https://sts.windows.net/{tenantId}/";

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "PolicyScheme";
            options.DefaultChallengeScheme = "PolicyScheme";
        })
        .AddJwtBearer("Internal", options =>
        {
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "email",
                AuthenticationType = "Internal"
            };
        })
        .AddJwtBearer("Entra", options =>
        {
            // Authority discovery pulls the signing keys from the tenant's JWKS
            // endpoint, so no Entra key material lives in configuration.
            options.Authority = entraV2Issuer;
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = [entraV2Issuer, entraV1Issuer],
                ValidateAudience = true,
                // Bare GUID for v2 tokens, App ID URI for v1 tokens.
                ValidAudiences = [entraAudience, $"api://{entraAudience}"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "preferred_username",
                RoleClaimType = "roles",
                AuthenticationType = "Entra"
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = HandleAuthenticationFailedAsync
            };
        })
        .AddPolicyScheme("PolicyScheme", "PolicyScheme", options =>
        {
            options.ForwardDefaultSelector = context =>
                SelectScheme(context.Request.Headers.Authorization.ToString());
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("can-read-quotes", policy => policy.RequireClaim("scope", "quotes.read"));
            options.AddPolicy("can-edit-quotes", policy => policy.RequireClaim("scope", "quotes.write"));
            options.AddPolicy("can-delete-quotes", policy => policy.RequireClaim("scope", "quotes.delete"));
        });

        services.AddTransient<IClaimsTransformation, ScopeClaimsTransformation>();
        services.AddSingleton<IAuthorizationHandler, MustOwnQuoteHandler>();
        services.AddSingleton<IAuthorizationHandler, MustOwnCollectionHandler>();

        return services;
    }

    // Decides which JWT bearer scheme should validate the request: pure string/claim
    // logic pulled out of the PolicyScheme's ForwardDefaultSelector so it's testable
    // without booting a host. Anything that isn't a readable, Entra-issued token falls
    // through to Internal, which rejects it with a proper 401.
    public static string SelectScheme(string? authorizationHeader)
    {
        var header = authorizationHeader ?? string.Empty;

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return "Internal";

        var token = header["Bearer ".Length..].Trim();
        var handler = new JwtSecurityTokenHandler();

        if (!handler.CanReadToken(token))
            return "Internal";

        var issuer = handler.ReadJwtToken(token).Issuer;

        // Match both the v2 and v1 Entra issuers. Anything else falls through
        // to Internal, which rejects it with a proper 401.
        return issuer.StartsWith("https://login.microsoftonline.com/", StringComparison.OrdinalIgnoreCase)
            || issuer.StartsWith("https://sts.windows.net/", StringComparison.OrdinalIgnoreCase)
            ? "Entra"
            : "Internal";
    }

    public static Task HandleAuthenticationFailedAsync(AuthenticationFailedContext ctx)
    {
        var env = ctx.HttpContext.RequestServices
            .GetRequiredService<IHostEnvironment>();

        // Exception type only, never the token itself.
        if (env.IsDevelopment())
            ctx.Response.Headers["x-auth-error"] = ctx.Exception.GetType().Name;

        return Task.CompletedTask;
    }
}
