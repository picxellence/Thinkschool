# EF vs Dapper: CollectionSummary read path

Two handlers produce the exact same `CollectionSummaryDto` (collection name,
owner, item count, ordered quote texts) from the same SQLite database:

- `CollectionSummaryQueryHandler` — a single EF Core LINQ projection
  (`Features/Collections/Queries/CollectionSummaryQueryHandler.cs`).
- `CollectionSummaryDapperQueryHandler` — hand-written SQL run through
  Dapper on `context.Database.GetDbConnection()`
  (`Features/Collections/Queries/CollectionSummaryDapperQueryHandler.cs`).

Correctness is enforced by `CollectionSummaryDapperQueryHandlerTests` (own
assertions plus a direct field-by-field comparison against the EF handler's
result) and by the benchmark test below, which asserts both results are
identical before and after the timed loop.

## SQL emitted

### EF Core (via `LogTo`, captured from a real run)

EF turns the single LINQ query — `Where(id).Select(new CollectionSummaryDto(...))`
with a nested `Items.Join(Quotes)` — into one round trip: a scalar subquery
for the item count, and a `LEFT JOIN` against a derived table for the
item/quote texts.

```sql
SELECT "c2"."Id", "c2"."Name", "c2"."OwnerUserId", "c2"."c", "s"."Text", "s"."CollectionId", "s"."QuoteId", "s"."Id"
FROM (
    SELECT "c"."Id", "c"."Name", "c"."OwnerUserId", (
        SELECT COUNT(*)
        FROM "CollectionItems" AS "c0"
        WHERE "c"."Id" = "c0"."CollectionId") AS "c"
    FROM "Collections" AS "c"
    WHERE "c"."Id" = @collectionId
    LIMIT 1
) AS "c2"
LEFT JOIN (
    SELECT "q"."Text", "c1"."CollectionId", "c1"."QuoteId", "q"."Id"
    FROM "CollectionItems" AS "c1"
    INNER JOIN "Quotes" AS "q" ON "c1"."QuoteId" = "q"."Id"
) AS "s" ON "c2"."Id" = "s"."CollectionId"
ORDER BY "c2"."Id", "s"."QuoteId", "s"."CollectionId"
```

One `Executed DbCommand` per call — confirmed by
`CollectionSummaryQueryHandlerTests.HandleAsync_EmitsExactlyOneSqlStatement`.

### Dapper (hand-written)

Two small statements rather than one multi-mapped result set — a single
header row plus a flat list is simpler to reason about than splitting one
result set on a key column:

```sql
-- 1. Header
SELECT "Id", "Name", "OwnerUserId"
FROM "Collections"
WHERE "Id" = @CollectionId

-- 2. CollectionItem -> Quote join, ordered
SELECT q."Text"
FROM "CollectionItems" ci
INNER JOIN "Quotes" q ON q."Id" = ci."QuoteId"
WHERE ci."CollectionId" = @CollectionId
ORDER BY ci."QuoteId"
```

`ItemCount` is `quoteTexts.Count` from query 2, not a separate `COUNT(*)` —
safe here because every `CollectionItem.QuoteId` is written by
`CreateCollectionCommandHandler`/`AddItem` against an existing quote, so
there are no orphaned references in this schema.

## Benchmark

`CollectionSummaryBenchmarkTests.EfAndDapper_ProduceIdenticalResults_AndAreBothTimed`:

- Seeds one collection with **50 items** (the `Collection.AddItem` cap).
- Runs each handler once as a warm-up (discarded), then times **1000**
  sequential calls per handler with `Stopwatch`, against the same open
  connection / `DbContext`.
- Asserts the DTOs from both handlers are field-for-field identical, both
  after warm-up and after the timed loop, so the comparison is apples-to-apples.
- Does **not** assert either handler is faster — it only reports numbers.

Measured on this machine, one run:

| Handler | Total (1000 calls) | Average per call |
|---|---|---|
| EF Core  | 284.16 ms | 0.2842 ms |
| Dapper   | 60.16 ms  | 0.0602 ms |

Dapper came out roughly 4.7x faster per call in this run. That gap is
consistent with what the SQL above suggests: EF's version does more work per
call — LINQ-expression-tree evaluation, query-plan cache lookup, and object
materialization through change-tracker-adjacent machinery even under
`AsNoTracking` — while Dapper's `QueryFirstOrDefaultAsync`/`QueryAsync` go
almost straight from the `DbDataReader` to a plain object via a cached IL
deserializer, at the cost of two round trips instead of one and no LINQ
query-composition safety net (the SQL is hand-maintained and drifts silently
if the schema changes). Absolute numbers will vary by machine and by how
warmed-up SQLite's own page cache is; the relative shape (EF has more
per-call overhead, Dapper has less abstraction) is the point, not the exact
milliseconds.
