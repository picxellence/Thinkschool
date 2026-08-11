# Thinkschool

Daily exercises building foundations across languages and runtimes.

## Day 1 — Hello in Two Languages

Same tiny program written in C# and TypeScript, run side by side to compare what each runtime requires.

- `day-1/hello-cs/` — C# console app (.NET 10 SDK)
- `day-1/hello-ts/` — TypeScript file run directly with Node 24 (no compile step needed)

### What I learned

C# needs a `.csproj` scaffold generated automatically by `dotnet new`. TypeScript on Node 24 runs directly with zero config — no `tsc`, no manifest file needed.

## Day 1 — QuotesApi (ASP.NET Core 10 Minimal API)

A real REST API for storing quotes, built with EF Core + SQLite.

- `day-1/QuotesApi/` — minimal API project

### Endpoints

- `GET /api/quotes?page=N&size=N` — list quotes, paginated
- `POST /api/quotes` — create a quote (`{author, text}`)
- `GET /api/quotes/{id}` — fetch one quote
- `DELETE /api/quotes/{id}` — delete a quote

### What it demonstrates

- EF Core migrations applied automatically at startup
- Repository pattern with `IQuoteRepository` (scoped DI)
- Input validation returning `ValidationProblemDetails`
- Global exception middleware returning `ProblemDetails`
- Cancellation tokens passed through to EF Core queries
- `Program.cs` kept lean by splitting setup into `AddInfrastructure()` and `MapQuoteEndpoints()`

### What surprised me

Sending a POST without an author or text should return a 400 instead of crashing.

# OrdersApi — Day 1 Refactor Exercise

This project is part of the Thinkschool Day 1 refactoring exercise.

## Objective

Create a deliberately poor ASP.NET Core API implementation and identify the design and maintainability problems before refactoring.

## Included Files

* `Controllers/OrderController.cs` — intentionally bad controller implementation
* `REFRACTOR_NOTES.md` — identified code smells, consequences, and intended fixes
* `PROMPT.md` — original prompt used to generate the legacy-style controller

## Intentional Problems in OrderController

* One giant POST endpoint
* Mixed validation, business logic, data access, and HTTP response shaping
* Empty `catch {}` blocks
* Synchronous EF Core calls inside an `async` method
* `object` return type instead of typed responses
* Off-by-one bug
* Potential null reference bug
* Multiple `SaveChanges()` calls
* No tests

## Build

```bash
dotnet build
```

The project builds successfully. Existing warnings are intentional and are part of the exercise.

## Next Refactoring Step

Planned architecture:

* Controller
* Service
* Repository
* DTOs
* Validation layer
* Unit tests
* Integration test

## Day 2 — Dependency Injection at Depth
Added a proper `IClock` abstraction to `QuotesApi`, registered as a Singleton, replacing direct `DateTime.UtcNow` calls.

- `day-1/QuotesApi/Services/IClock.cs` + `SystemClock.cs` — clock abstraction
- Wired through `Collection` → `CollectionItem` → the `/collections/{id}/items` endpoint
- `day-1/QuotesApi.Tests/` — test using a `FakeClock` to prove timestamps are deterministic and testable

### What it demonstrates
- Correct use of Transient / Scoped / Singleton DI lifetimes
- Why hidden dependencies (like `DateTime.UtcNow`) make code hard to test
- Constructor injection over creating dependencies inline

## Day 2 — async/await with Cancellation Through Layers
Verified and proved that `CancellationToken` flows correctly through every layer of the Collection endpoints: endpoint → repository → EF Core.

- `day-1/QuotesApi.Tests/CancellationTests.cs` — integration test using `WebApplicationFactory` that cancels a token mid-request and confirms the operation is not silently completed

### What it demonstrates
- `CancellationToken` must be manually threaded through every async layer — it isn't automatic
- Testing cancellation behavior with a real in-memory HTTP client instead of guessing

## Day 2 — Test the Domain Layer
Pure unit tests for the `Collection` aggregate's business rules, using xUnit + FluentAssertions. No database, no HTTP — just the aggregate's own logic.

- `day-1/Tests.Domain/` — test project

### Invariants tested
- Empty name throws
- Name over 80 characters throws
- Adding a 51st item throws
- Adding a duplicate quote ID throws
- Removing a non-existent item throws
- Add-then-remove leaves zero items

### What it demonstrates
- Domain/aggregate logic can be tested in isolation, fast and without infrastructure
- FluentAssertions for more readable test assertions
- Test result: 6 passed, 0 failed


## Day 2 — AI-assisted Refactor: Anemic to Rich
Refactored the `Quote` entity from an anemic model (plain public properties) to a rich domain model that enforces its own rules. Done on branch `anemic-to-rich` using GitHub Copilot Chat.

- `day-1/QuotesApi/Models/Quote.cs` — private setters, `IsDeleted` soft-delete flag, static factory
- `day-1/QuotesApi/Models/Result.cs` — generic success/failure wrapper used instead of exceptions
- `day-1/QuotesApi/WHY.md` — what the rich model buys over the anemic one

### Invariants enforced
- Author: 1–200 characters
- Text: 1–1000 characters
- Text can never be changed after creation — only soft-deleted via `IsDeleted`
- Construction only through `Quote.Create(author, text)`, returning `Result<Quote>`

### What changed to support it
- `EndpointExtensions.cs` — `POST /api/quotes` now calls `Quote.Create()` instead of an object initializer
- `QuoteRepository.cs` — delete calls `quote.SoftDelete()`; reads filter out soft-deleted quotes
- `QuotesDbContext.cs` — added matching max-length constraints
- New EF Core migration (`AddQuoteRichDomainModel`) for the `IsDeleted` column

### Verified with curl
- Valid quote → 201 Created
- Empty author → 400, "Author is required."
- Text over 1000 chars → 400, "Text must be 1-1000 characters."

## Day 2 — Implement JWT Auth (Own Issuer)
Added self-contained authentication to `QuotesApi`: users log in with email/password, receive a signed JWT access token and a refresh token, and protected endpoints reject requests without a valid token.

- `day-1/QuotesApi/Models/User.cs` — entity with BCrypt-hashed passwords
- `day-1/QuotesApi/Services/JwtTokenService.cs` — mints HS256-signed access tokens + random refresh tokens
- `POST /api/auth/login` — takes `{email, password}`, returns `{accessToken, refreshToken, expiresIn}`
- `POST` and `DELETE` on `/api/quotes` now require a valid token (`.RequireAuthorization()`); `GET` stays open

### What it demonstrates
- Password hashing with BCrypt.Net-Next — never rolling your own
- JWT signing with HS256, key loaded from configuration, never hardcoded
- `AddAuthentication().AddJwtBearer()` + `AddAuthorization()` middleware pipeline
- Access tokens (short-lived, 15 min) vs refresh tokens (long-lived, opaque random string)

### Verified with curl
- No token → 401 Unauthorized, `WWW-Authenticate: Bearer`
- Valid token → 201 Created
- Expired token: middleware configured (`ValidateLifetime: true`) but not confirmed live