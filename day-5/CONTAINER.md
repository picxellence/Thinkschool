# Containerizing QuotesApi with .NET 10's built-in container support

No Dockerfile — this uses `dotnet publish /t:PublishContainer` (SDK container support,
GA since .NET 8) driven entirely by MSBuild properties in the `.csproj`.

## 1. Base image verification

The task assumed `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` exists. Checked against the
real registry rather than guessing:

```
curl -s https://mcr.microsoft.com/v2/dotnet/aspnet/tags/list
# -> tags list includes "10.0-alpine"

docker manifest inspect mcr.microsoft.com/dotnet/aspnet:10.0-alpine
```

```json
{
   "schemaVersion": 2,
   "mediaType": "application/vnd.docker.distribution.manifest.list.v2+json",
   "manifests": [
      { "digest": "sha256:f95807c53deaba56064fd7c490378869e9fa94c787e8ff7a028ea893d4d9fcea", "platform": {"architecture": "amd64", "os": "linux"} },
      { "digest": "sha256:838c089345433ac38b9ffd0e0667fb86e92504afafad3be6e24d96129495f883", "platform": {"architecture": "arm", "os": "linux", "variant": "v7"} },
      { "digest": "sha256:971933f58b90f2f20a43df28b97a249aece87e5e3a1e250a04dd8a4fb8e8dbe5", "platform": {"architecture": "arm64", "os": "linux"} }
   ]
}
```

`10.0-alpine` is real and multi-arch. No substitution needed.

## 2. The `/health` endpoint

Added `QuotesApi/HealthChecks/DatabaseHealthCheck.cs` (checks `QuotesDbContext.Database.CanConnectAsync`)
and wired it in `Program.cs`:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
...
app.MapHealthChecks("/health").AllowAnonymous();
```

`AllowAnonymous()` is explicit and load-bearing — a container probe carries no bearer token,
and this project's default is authenticated unless an endpoint opts out.

## 3. Container properties added to `QuotesApi.csproj`

```xml
<ContainerImageName>quotes-api</ContainerImageName>
<ContainerImageTag>0.1.0</ContainerImageTag>
<ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:10.0-alpine</ContainerBaseImage>
```

The build emits one deprecation warning, left as specified since the task named this property
explicitly:

```
warning CONTAINER003: The property 'ContainerImageName' was set but is obsolete - please use 'ContainerRepository' instead.
```

## 4. Build

The task's exact command, `dotnet publish --os linux --arch x64 /t:PublishContainer`, resolves
to RID `linux-x64` (glibc) and **produces a container that crashes on startup** — see "Problems
found" below. Fixed by targeting the musl RID instead, keeping the same alpine base image:

```
$ dotnet publish --os linux-musl --arch x64 /t:PublishContainer
```

```
  Determining projects to restore...
  Restored /Users/ujjwalsrivastava/thinkschool/day-5/QuotesApi/QuotesApi.csproj (in 1.04 sec).
  QuotesApi -> /Users/ujjwalsrivastava/thinkschool/day-5/QuotesApi/bin/Release/net10.0/linux-musl-x64/QuotesApi.dll
  QuotesApi -> /Users/ujjwalsrivastava/thinkschool/day-5/QuotesApi/bin/Release/net10.0/linux-musl-x64/publish/
  Building image 'quotes-api' with tags '0.1.0' on top of base image 'mcr.microsoft.com/dotnet/aspnet:10.0-alpine'.
  Pushed image 'quotes-api:0.1.0' to local registry via 'docker'.
```

**Image size:**

```
$ docker images quotes-api:0.1.0
IMAGE              ID             DISK USAGE   CONTENT SIZE   EXTRA
quotes-api:0.1.0   adf4511fa166        195MB         59.2MB

$ docker inspect quotes-api:0.1.0 --format "Size: {{.Size}} bytes"
Size: 59200670 bytes
```

**59.2 MB** (content size — the actual pushed image; "disk usage" includes shared base layers
already present locally). Disk stayed healthy throughout: 9.6 GiB free before and after.

## Problems found (and fixed) along the way

Two real failures surfaced getting from "image builds" to "image runs correctly." Both are
genuine footguns of this exact stack (SQLite + Alpine + non-root container), not something to
paper over silently, so they're recorded here with the actual crash output.

### a) glibc/musl mismatch — `--os linux --arch x64` is the wrong RID for an alpine base

First build used the task's literal command, `--os linux --arch x64`. Container crashed
immediately:

```
Unhandled exception. System.TypeInitializationException: The type initializer for 'Microsoft.Data.Sqlite.SqliteConnection' threw an exception.
 ---> System.DllNotFoundException: Unable to load shared library 'e_sqlite3' or one of its dependencies.
Error relocating /app/libe_sqlite3.so: fcntl64: symbol not found
```

`--os linux --arch x64` resolves to RID `linux-x64`, which pulls the **glibc**-linked native
SQLite binary. Alpine uses **musl** libc, so the symbol resolution fails at process start —
before the app ever binds a port. Confirmed the fix by checking the NuGet cache: the
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 package already ships a `runtimes/linux-musl-x64/` binary;
it just wasn't the one selected. Rebuilding with `--os linux-musl --arch x64` (same alpine base
image, same csproj) picked the correct binary and the native-library error disappeared.

Asked before making this change since it alters the exact command specified in the task —
confirmed switching to `--os linux-musl` over switching the base image to a glibc distro.

### b) Non-root `/app` isn't writable — SQLite can't create its file

With the musl fix in place, the container still crashed, later in startup (during
`db.Database.Migrate()`):

```
07:10:04 [ERR] [TraceId:] An error occurred using the connection to database 'main' on server 'quotes.db'.
Unhandled exception. Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 14: 'unable to open database file'.
```

.NET's container images run as a non-root user (`app`, uid 1654) by default, and `/app` is
owned by `root:root` (`drwxr-xr-x`) — the app user can read the published binaries there but
can't create new files in that directory. The connection string default,
`Data Source=quotes.db`, is a relative path that resolves to the (unwritable) working
directory `/app`. Verified `/tmp` is world-writable inside the image
(`drwxrwxrwt root root`) and pointed the connection string there via
`ConnectionStrings__Default` — no code or image change needed, just one more environment
variable at `docker run` time.

## 5. Run

```
$ docker run -d --name quotes-api-test -p 8080:8080 \
  -e Jwt__Key="throwaway-not-a-real-secret-0123456789ABCDEF" \
  -e Entra__TenantId="00000000-0000-0000-0000-000000000000" \
  -e Entra__Audience="11111111-1111-1111-1111-111111111111" \
  -e ConnectionStrings__Default="Data Source=/tmp/quotes.db" \
  quotes-api:0.1.0
```

All four values above are throwaway placeholders — nothing baked into the image, nothing from
this machine's real user-secrets. `Jwt:Issuer` and `Jwt:Audience` aren't overridden because
they're non-secret defaults already in `appsettings.json` ("QuotesApi" / "QuotesApiClients");
only the three `[Required]`-validated-on-start values plus the writable-DB-path workaround are
supplied here.

Startup log (Production environment — no dev user seeded, migrations ran clean):

```
07:11:53 [INF] [TraceId:] Applying migration '20260812122445_AddCollectionOwnership'.
07:11:53 [INF] [TraceId:] Executed DbCommand (1ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
DELETE FROM "__EFMigrationsLock";
07:11:53 [WRN] [TraceId:] Storing keys in a directory '/home/app/.aspnet/DataProtection-Keys' that may not be persisted outside of the container.
07:11:53 [INF] [TraceId:] Now listening on: http://[::]:8080
07:11:53 [INF] [TraceId:] Application started. Press Ctrl+C to shut down.
07:11:53 [INF] [TraceId:] Hosting environment: Production
07:11:53 [INF] [TraceId:] Content root path: /app
```

## 6. Proof it's the real app, not just a 200 from `/health`

```
$ curl -s -D - http://localhost:8080/health
HTTP/1.1 200 OK
Content-Type: text/plain
X-Trace-Id: 910d3074180cf89ac222b6703c9f3811

Healthy
```

```
$ curl -s -D - http://localhost:8080/api/quotes
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
X-Trace-Id: 6a116ac001cabc6102489f67883b4933

[]
```

`GET /api/quotes` is `QuoteRepository.GetPagedAsync` running a real paged EF Core query
against the migrated SQLite database at `/tmp/quotes.db` inside the container — `[]` because
the container starts with an empty (freshly migrated) database, not a stub. As a second
signal that the full pipeline (routing, auth middleware, EF Core) is live, an unauthenticated
write was correctly rejected rather than 200'ing:

```
$ curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:8080/api/quotes \
  -H "Content-Type: application/json" -d '{"author":"a","text":"b"}'
401
```

## Cleanup

Test container stopped and removed after verification (`docker rm -f quotes-api-test`); the
`quotes-api:0.1.0` image was left in the local Docker image store. Nothing was committed to
git.
