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

## Day 3 — Wire Entra ID as the Identity Provider

Added Microsoft Entra ID as a second identity provider alongside the
self-issued JWT from Day 2. Both work at the same time, on the same
endpoints, with no changes to endpoint code.

- `day-3/QuotesApi/Program.cs` — three authentication schemes registered
- `"Internal"` — validates HS256 tokens minted by `JwtTokenService`
- `"Entra"` — validates tenant-issued tokens, signing keys pulled live
  from the tenant's JWKS endpoint via `Authority`
- `"PolicyScheme"` — an `AddPolicyScheme` router set as the default

### How the routing works

The policy scheme reads the incoming token's `iss` claim without validating
it, then forwards to whichever scheme can actually verify the signature.
Tokens issued by `login.microsoftonline.com` or `sts.windows.net` go to
`Entra`; everything else, including malformed tokens and missing headers,
falls through to `Internal`, which rejects them with a normal 401.

Because the default authorization policy resolves through the router, every
endpoint that already called `.RequireAuthorization()` started accepting
Entra tokens without being touched.

### What it demonstrates

- A policy scheme is a router, not a validator
- No Entra key material in configuration — `Authority` discovery handles it
- Tolerates both token versions: v1 (`sts.windows.net` issuer, `api://{guid}`
  audience) and v2 (`/v2.0` issuer, bare GUID audience)
- `AuthenticationType` set explicitly per scheme, since JwtBearer otherwise
  reports `AuthenticationTypes.Federation` for both
- `Jwt:Key` moved out of `appsettings.json` into user-secrets
- Exception middleware moved ahead of the auth middleware so it wraps it

### Azure setup

App registration with an Application ID URI of `api://{client-id}`, a
delegated `access_as_user` scope, and the Azure CLI client id
(`04b07795-8ddb-461a-bbee-02f9e1bf7b46`) added as an authorized client
application so `az account get-access-token` can request a token.

### Verified with curl

- `GET /api/auth/whoami` with a login token → `"validatedBy": "Internal"`
- `GET /api/auth/whoami` with an `az` token → `"validatedBy": "Entra"`,
  `"scopes": "access_as_user"`
- `POST /api/quotes` with an Entra token → 201 Created, on an endpoint
  written before Entra existed

## Day 3 — Authorization Policies and Claims

Authentication answers "who is this." This task makes the API use that
answer. Two mechanisms, because two different kinds of rule are needed.

- `day-3/QuotesApi/Authorization/ScopeClaimsTransformation.cs`
- `day-3/QuotesApi/Authorization/MustOwnQuoteRequirement.cs`

### Claim-based policies

Decided from the token alone, before any endpoint code runs:

- `can-read-quotes` → requires `scope=quotes.read`
- `can-edit-quotes` → requires `scope=quotes.write`
- `can-delete-quotes` → requires `scope=quotes.delete`

Applied as `.RequireAuthorization("can-edit-quotes")` on `POST /api/quotes`
and `.RequireAuthorization("can-delete-quotes")` on `DELETE`.

### Normalising claims across schemes

Self-issued tokens carry `scope` claims directly. Entra uses `scp` — a
single space-separated string for delegated permissions — and `roles` for
app-only ones. `ScopeClaimsTransformation` implements `IClaimsTransformation`
and translates both into `scope` claims once per request, so the policies
never need to know which scheme authenticated the caller.

### Resource-based ownership

"Can this caller delete *this* quote" can't be answered from a token, since
it depends on a database row. `MustOwnQuoteRequirement` plus
`MustOwnQuoteHandler` is checked imperatively inside the DELETE endpoint,
after the quote is loaded, via `IAuthorizationService.AuthorizeAsync`.
`Quote` gained a nullable `CreatedByUserId`, stamped on creation from the
caller's `oid` or `sub` claim (migration `AddQuoteOwnership`).

### What it demonstrates

- Policies over roles — `RequireClaim` is portable, `RequireRole("admin")` is
  brittle
- 401 means "I don't know who you are"; 403 means "I know, and no"
- Declarative claim checks and imperative resource checks solve different
  problems and both are needed
- DELETE loads first, so a missing quote is 404 and an unowned one is 403

### Tests

`day-3/QuotesApi.Tests/AuthorizationPolicyTests.cs` — 8 passing, against the
real pipeline via `WebApplicationFactory` on a throwaway SQLite file:

- POST with `quotes.write` → 201
- POST with only `quotes.read` → 403
- POST with no token → 401
- DELETE by the creator → 204
- DELETE by a different user holding `quotes.delete` → 403
- DELETE by the creator on a token lacking `quotes.delete` → 403

### Known gaps

- Every issued token carries all three scopes, so scope isn't separating
  users yet — ownership is doing the work
- `/collections` still has no authorization and takes `OwnerId` from the
  request body

## Day 3 — Lock Down the API End-to-End

Closed the gaps the previous task left. Authentication knew who the caller was;
this task made the API use that answer everywhere.

- `day-3/QuotesApi/Authorization/MustOwnCollectionRequirement.cs`

### The IDOR

`/collections` had no authorization at all, and `CreateCollectionRequest`
carried `OwnerId` in the request body. Anyone could create a collection owned
by anyone, and anyone could mutate any collection by id. Fixed by dropping
`OwnerId` from the request model and taking the owner from the caller's
`oid`/`sub` claim, with a `MustOwnCollectionRequirement` enforced on the
add-item and remove-item endpoints after the collection is loaded.

`Collection.OwnerId` is an `int` predating auth and doesn't match the claim
type. Rather than rewrite a table with data in it, a nullable
`OwnerUserId` was added alongside and `OwnerId` left as a dead column — a
deliberate shortcut, recorded rather than hidden.

### Two pre-existing bugs fixed

- `GET /api/quotes` declared `int page, int size` with no defaults, so a
  request with no query string failed parameter binding with a 400
- The remove-item endpoint had no try/catch, so a missing item surfaced as a
  500 instead of a 404

### Tests

Anonymous → 401. Authenticated with the wrong scope → 403. Authenticated with
the right scope → 200/201. Expired token → 401. Refresh-chain reuse → 401,
with reuse detection firing and revoking the whole family.

## Day 3 — Unit Tests with xUnit and FluentAssertions

- `day-3/Quotes.Tests.Unit/` — 40 tests, 990 ms, no I/O

One test class per production class, `Method_StateUnderTest_ExpectedBehavior`
naming, strict AAA, no constructor setup. FluentAssertions pinned to 7.2.0
since 8.x is licence-gated. Covers `Collection`, `User`, `RefreshToken` (with
a hand-written `FakeClock`), `ScopeClaimsTransformation`, both ownership
handlers, and `JwtTokenService`.

### A bug the unit tests found

`ScopeClaimsTransformation` enumerated `identity.FindAll("roles")` while
calling `AddClaim` on the same identity, which throws
`InvalidOperationException`. Dormant in practice because internal tokens
short-circuit on an existing `scope` claim, but any Entra app-only token with
`roles` and no `scp` would have hit it. The integration suite never reached
that path.

### Deviation from the brief

The brief asked for tests of a `Quote.Create` factory. `Quote` is still anemic
in this repo — validation lives in the endpoint. `Collection` is the rich model
with a factory and invariants, so that was tested instead.

## Day 3 — Integration Tests with WebApplicationFactory

- `day-3/Quotes.Tests.Integration/` — renamed from `QuotesApi.Tests`

Switched from a shared `IClassFixture` to a factory per test, so each test gets
its own host and its own SQLite file. Added a fake `IClock` in the test host,
a ProblemDetails assertion on validation failure, and a pending-migrations
check.

### The cost of real isolation

Measured on the same 13 tests before adding anything: shared fixture ≈ 2.31s,
per-test factory ≈ 5.8s. Roughly 2.5× slower, all of it host boot and migrate
paid per test. Worth it — the old suite only passed because tests used
distinct user ids to dodge each other's rows.

Also found that `Dispose` only deleted the main `.db` file, not SQLite's `-wal`
and `-shm` sidecars. Harmless with one shared file; per-test isolation would
have littered temp.

### A test that was removed

A `GET /collections/{id}` 404 test was written and then deleted. That endpoint
doesn't exist, so the test asserted on ASP.NET's unmatched-route handling
rather than on any application code.

## Day 3 — Testcontainers with Real SQL Server

- `day-3/Quotes.Tests.Integration/` — SQL Server 2022 as an `ICollectionFixture`

Container started once for the whole suite, with per-test isolation moving from
separate SQLite files to uniquely named databases on the shared instance. A
config-driven provider switch keeps SQLite as the default so the app runs
locally unchanged, with SQL Server migrations in a separate output directory.

**Not verified.** The machine ran out of disk pulling the SQL Server image, and
the pull failure surfaced as `DockerImageNotFoundException` rather than a disk
error. Everything compiles; nothing has been proven to run.

## Day 4 — CI with GitHub Actions

- `.github/workflows/ci.yml`

Triggers on push and on pull requests to `main`. Restores, builds, runs both
test projects with TRX loggers and `--collect:"XPlat Code Coverage"`, merges
the reports with ReportGenerator excluding EF migrations, enforces a line
coverage threshold, and uploads results and coverage as artifacts with
`if: always()` so they survive a failure.

### Proving the gate works

`--collect` produces a number, not a gate — nothing fails on low coverage
unless you write the check. To confirm the check wasn't silently passing on an
empty grep, the threshold was temporarily raised to 95%, CI went red, then it
was restored to 70% and CI went green. Same code both times.

Branch protection requiring the `test` check was not configured — no admin
rights on the repository in the org.

## Day 4 — Drive Coverage to 80%

83.8% line / 69.3% branch → **99.8% line / 95.4% branch**.

The report's Risk Hotspots flagged one method: `Program.<Main>$` with
cyclomatic complexity 36 — the entire auth setup living in top-level
statements. Rather than write host-booting tests to reach it, the wiring was
extracted into `AuthenticationExtensions.AddApiAuthentication`, with the
`ForwardDefaultSelector` pulled out as a pure static `SelectScheme(string?)`.
The issuer routing the whole dual-scheme design rests on is now a plain unit
test. `Program.cs` went from ~155 lines to ~15.

### The finding

The two collection-item endpoints had zero coverage. Not a missed branch — the
whole endpoints. Those are exactly the ones that gained `MustOwnCollection` the
day before, so the 403 path shipped without ever being executed.

### Deleted rather than covered

- `CollectionRepository.DeleteAsync` — nothing calls it, there is no
  delete-collection endpoint
- The `catch (ArgumentException)` in create-collection — the endpoint's own
  validation is identical to `Collection.SetName`'s, so it is unreachable

`RefreshToken.Id` left uncovered on purpose; nothing reads it, and a test whose
only job is calling a getter is not a test.

### Bug found while covering

`ExceptionMiddleware` sets `ContentType` to `application/problem+json`, then
`WriteAsJsonAsync` overwrites it to `application/json`. The test asserts actual
behaviour and the mismatch is flagged rather than silently patched.

## Day 4 — Serilog with Correlation IDs

- `day-4/QuotesApi/Extensions/SerilogExtensions.cs`
- `day-4/QuotesApi/Middleware/CorrelationIdMiddleware.cs`

Structured templates throughout, per-category levels in configuration, and a
middleware that pushes the trace id into `LogContext` and echoes it as an
`X-Trace-Id` response header. Registered before `UseSerilogRequestLogging` and
`ExceptionMiddleware`, so exception logs carry the id too — otherwise the one
line you most want to trace is the one without one.

One `GET /api/quotes` produces nine correlated lines: the endpoint log, EF
Core's command lifecycle, and the request-completion entry. Startup and
migration lines correctly show it blank.

### Test noise

The integration suite boots the host 40 times. A `[ModuleInitializer]` sets the
Serilog minimum level to Warning before any host starts; one test opts back in
with a capturing `ILogEventSink`.

### An order-dependent test CI caught

`CancellationTests` used a raw `WebApplicationFactory` and relied on another
test class having already set the config environment variables. It passed
locally and failed on the runner. Confirmed empirically that
`ConfigureAppConfiguration` can't replace the env-var workaround: `Program.cs`'s
top-level statements run inside `DeferredHostBuilder.Build()`, so config sources
fold in after `AddApiAuthentication` has already read them.

## Day 4 — OpenTelemetry Tracing

- `day-4/QuotesApi/Extensions/TracingExtensions.cs`

ASP.NET Core and EF Core instrumentation, with a console exporter gated by
configuration so tests can silence it without disabling tracing. One request
produces a server span with the EF query as a child, `ParentSpanId` matching
exactly.

### Two things named TraceId that weren't the same value

`X-Trace-Id` and the custom `LogContext` property held `ctx.TraceIdentifier`,
while Serilog's built-in `{TraceId}` token binds to `Activity.Current`, which
OTel now populates. Different strings — so the id handed to a customer matched
nothing they could search for. Worse, the logging test still passed, because it
asserted on the custom property rather than the field the console renders.
Fixed by sourcing both from `Activity.Current?.TraceId` and changing the test to
assert on `LogEvent.TraceId`.

## Day 4 — Azure Application Insights

- `day-4/QuotesApi/Configuration/ApplicationInsightsOptions.cs`
- `day-4/QuotesApi/KQL.md`

Azure Monitor exporter registered only when a connection string is present —
the integration suite boots the host 40 times and none of them may call Azure.
Connection string in user-secrets locally, Key Vault reference in App Service,
empty placeholder in `appsettings.json`.

`operation_Id` in App Insights is the same W3C trace id the API returns in
`X-Trace-Id`, so a customer quoting that header leads straight to their request
across `traces`, `requests` and `dependencies`.

Alert on average server response time over 500ms, five-minute window, email
action group. App-wide rather than scoped to `POST /api/quotes` — the Server
response time metric has no Request name dimension, so per-endpoint alerting
needs a log-search rule instead.

### Worth knowing

A malformed connection string crashes the app at startup rather than just
disabling telemetry. Defensible as fail-fast, but in App Service it means a bad
config value takes the API down instead of losing telemetry.

## Day 4 — Typed Configuration with IOptions

- `day-4/QuotesApi/Configuration/JwtOptions.cs`, `EntraOptions.cs`

Loose `config["..."]` reads replaced with records bound via
`AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`.
`AccessTokenMinutes`/`RefreshTokenDays` became `TimeSpan` properties, removing
three `int.Parse` calls — two of them inline in `EndpointExtensions`, which
would have been missed if the rename hadn't broken the build.

`AddApiAuthentication` runs before `builder.Build()`, so `IOptions<T>` isn't
resolvable inside it and the section is bound manually there. That guard only
catches a missing section; `ValidateOnStart` catches missing or empty
individual properties at host startup, which is the right layer.

`ValidateOnStart` was not taken on trust. Four tests build a real generic host
through the actual registration path and call `host.Start()`, asserting
`OptionsValidationException` for a missing key, an empty-string key, and a
missing tenant id. Registration compiling is not the same as validation firing.

## Day 5 — Diagnose a Slow Endpoint from Traces

- `day-5/DIAGNOSIS.md`, `day-5/docs/trace-before.png`, `docs/trace-after.png`

An N+1 was introduced deliberately in `CollectionRepository.GetByIdAsync`, then
found from the trace and fixed. Kept as two commits so the fix is a readable
diff.

| | Trace | Spans | Duration |
|---|---|---|---|
| Before | `9388ba6` | 23 | 9.33 ms |
| After | `3569ee6` | 4 | 2.85 ms |

The waterfall gave it away before reading any code — one parent span with twenty
near-identical children, each `SELECT ... FROM "Quotes" WHERE "Id" = @item_QuoteId`.
Fixed with a single `.Include(c => c.Items)`.

Two things the trace surfaced. The per-item `Quote` fetches were dead code —
`CollectionItem` has no `Quote` navigation, so nothing consumed the results. And
watching span counts climb 20, 21, 22, 23 across consecutive calls made the
scaling concrete: the N in N+1, visible in a list view.

On SQLite the whole N+1 cost about 4ms of database time, under any alert
threshold. Against a network database at 5ms per round trip the same code is
~115ms. The trace shows the shape; the shape is what scales.

### A missing exporter

Pointing the Aspire dashboard at the app produced nothing. Day 4 wired the
console exporter and Azure Monitor but never added `AddOtlpExporter()` —
invisible until something needed it, because the console exporter was doing all
the visible work.

## Day 5 — Container Image from dotnet publish

- `day-5/CONTAINER.md`

59.2 MB Alpine image, no Dockerfile. Also added a `/health` endpoint, since the
app had none — `AddHealthChecks` with a `DatabaseHealthCheck` calling
`CanConnectAsync`, mapped anonymous because a container probe carries no token.

Two failures before it ran.

`--os linux --arch x64` resolves to the glibc RID; alpine is musl. The image
built and then crashed with `Error relocating /app/libe_sqlite3.so: fcntl64:
symbol not found`. `SQLitePCLRaw` ships a musl build; it just wasn't selected.
Fixed with `--os linux-musl --arch x64`. Only affects apps with native
dependencies.

Then SQLite couldn't create its file. `/app` is root-owned and the .NET base
images run as non-root. Pointed the connection string at `/tmp` via an
environment variable.

Verified with three curls: `/health` → 200, `GET /api/quotes` → 200 `[]`, and
`POST /api/quotes` → 401. The third is the one that matters — a health endpoint
returning 200 only proves a process is listening.

## Day 5 — Azure Container Apps Environment

- `day-5/containerapp-env.json`

Resource group and a Container Apps environment in Central India. The
environment holds networking, the log destination and the KEDA autoscaling
engine; apps are deployments into it, which is why revisions can coexist.

It auto-created its own Log Analytics workspace because none was passed —
easy to end up paying for ingestion twice without noticing.

## Day 5 — Deploy via azd

Live at `quotes-api.agreeablecliff-bba27145.centralindia.azurecontainerapps.io`.

Four things went wrong between "azd up succeeded" and "the app responded".

1. `MaxNumberOfRegionalEnvironmentsInSubExceeded` — the subscription allows one
   Container Apps environment per region, and the previous task had used it.
2. A transient 412 on the first deploy step, cleared by re-running.
3. `ImagePullBackOff`. azd pushed to the repository `quotes-api` but wrote
   `day-5/quotes-api-thinkschool-day5` into the container app's image
   reference. Both `azd up` and `azd deploy` reported SUCCESS throughout,
   because they verify the push and the ARM update independently and never
   check that the tag they wrote resolves.
4. The musl/glibc mismatch again. The csproj set an alpine base image but azd
   publishes for the glibc RID. The previous task's fix lived in a command-line
   flag, so azd's own build inherited the original mistake. Switched
   `ContainerBaseImage` to the Debian variant.

Config reaches the container as environment variables; nothing is baked into
the image.

## Day 5 — Verify in App Insights with KQL

- `day-5/docs/kql-endpoint-latency.png`, saved as a function `EndpointLatency`

| name | count | p50 | p99 |
|---|---|---|---|
| GET /api/quotes/ | 16 | 2.38 | 572.97 |
| GET /health | 11 | 1.58 | 187.94 |
| POST /api/auth/login | 1 | 26.06 | 26.06 |
| GET /api/quotes/{id:int} | 5 | 1.84 | 24.74 |
| POST /api/quotes/ | 6 | 0.53 | 9.41 |

A p99 of 573ms against a p50 of 2.4ms is a 240× spread on one endpoint doing
identical work. That's cold start, not variance — one request paid for JIT, EF
model building and the first SQLite connection. With 16 samples the p99 is just
"the slowest single request". Percentiles need volume before they mean anything.

`POST /api/auth/login` is 26ms here but 344ms locally. Bcrypt is the difference:
in Azure the seeded dev user doesn't exist, so the lookup 401s before hashing.
The fast number is a failure path.

### Why nothing arrived at first

azd injects `APPLICATIONINSIGHTS_CONNECTION_STRING`; `TracingExtensions` reads
`ApplicationInsights:ConnectionString` through `IOptions`. Different keys, so
the exporter was never registered. The app ran perfectly and emitted nothing.

## Day 5 — Polly Resilience on HTTP Calls

- `day-5/RESILIENCE.md`, `day-5/QuotesApi/Clients/`

The API made no outbound HTTP calls, so a small typed client was added —
`GET /api/quotes/random` fetching from zenquotes.io — to have a real dependency
to protect.

Pipeline order is total timeout → retry (exponential, jittered) → circuit
breaker, with the numbers in a `ResilienceOptions` record bound from
configuration rather than hardcoded. Every retry, circuit-open and
circuit-close writes a structured log line.

The endpoint catches `HttpRequestException`, `TimeoutRejectedException` and
`BrokenCircuitException` and returns 503. That third one matters — once the
breaker opens, calls throw a different exception type immediately, so missing
it produces 500s exactly when the breaker is doing its job.

### Tests

Three, against the real pipeline through a stub `DelegatingHandler`, no network:

- 503, 503, 200 → exactly 3 attempts, succeeds
- 503 × 4 → retries exhausted, fails within 5s rather than hanging
- circuit opens → the circuit-breaker-opened log actually fires

The second is the one worth having. A resilience config can look correct and
still block for 40 seconds per request under sustained failure, which is worse
than failing straight away.

### Known gap

The retry policy treats every failure as transient. A 400 gets retried three
times before failing — wasted time and load on an error that will never succeed.