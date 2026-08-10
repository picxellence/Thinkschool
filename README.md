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

