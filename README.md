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
