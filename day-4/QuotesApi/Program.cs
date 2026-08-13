using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Middleware;
using QuotesApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using QuotesApi.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();

// Fail fast at startup instead of producing a null reference on first request.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

var tenantId = builder.Configuration["Entra:TenantId"]
    ?? throw new InvalidOperationException("Entra:TenantId is not configured.");
var entraAudience = builder.Configuration["Entra:Audience"]
    ?? throw new InvalidOperationException("Entra:Audience is not configured.");

// v2 issuer once the manifest has requestedAccessTokenVersion = 2.
var entraV2Issuer = $"https://login.microsoftonline.com/{tenantId}/v2.0";
// v1 issuer, which is what the az CLI returns if it is still on version 1.
var entraV1Issuer = $"https://sts.windows.net/{tenantId}/";

builder.Services.AddAuthentication(options =>
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
        OnAuthenticationFailed = ctx =>
        {
            var env = ctx.HttpContext.RequestServices
                .GetRequiredService<IHostEnvironment>();

            // Exception type only, never the token itself.
            if (env.IsDevelopment())
                ctx.Response.Headers["x-auth-error"] = ctx.Exception.GetType().Name;

            return Task.CompletedTask;
        }
    };
})
.AddPolicyScheme("PolicyScheme", "PolicyScheme", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var header = context.Request.Headers.Authorization.ToString();

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
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("can-read-quotes", policy => policy.RequireClaim("scope", "quotes.read"));
    options.AddPolicy("can-edit-quotes", policy => policy.RequireClaim("scope", "quotes.write"));
    options.AddPolicy("can-delete-quotes", policy => policy.RequireClaim("scope", "quotes.delete"));
});

builder.Services.AddTransient<IClaimsTransformation, ScopeClaimsTransformation>();
builder.Services.AddSingleton<IAuthorizationHandler, MustOwnQuoteHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, MustOwnCollectionHandler>();

var app = builder.Build();

// Exception handling goes outermost so it also wraps the auth middleware.
app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    db.Database.Migrate();

    // Seeded credentials must never be created in a deployed environment.
    if (app.Environment.IsDevelopment() && !db.Users.Any())
    {
        db.Users.Add(User.Create("test@example.com", "Password123!"));
        db.SaveChanges();
    }
}

app.MapQuoteEndpoints();

// Proof endpoint for the Day 3 exercise: reports which scheme validated the
// request, so one curl distinguishes an internal token from an Entra one.
app.MapGet("/api/auth/whoami", (ClaimsPrincipal user) => Results.Ok(new
{
    validatedBy = user.Identity?.AuthenticationType,
    subject = user.FindFirst("oid")?.Value ?? user.FindFirst("sub")?.Value,
    name = user.Identity?.Name,
    scopes = user.FindFirst("scp")?.Value
}))
.RequireAuthorization();

app.Run();

// Required for WebApplicationFactory<Program> in QuotesApi.Tests.
public partial class Program { }