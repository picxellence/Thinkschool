# Day 10 — query translation: whole entity vs. projection vs. client eval

SQLite, `Quote` table (`Id`, `Author`, `Text`, `CreatedByUserId`), seeded with 304 rows.
SQL captured via `LogTo(output.WriteLine, LogLevel.Information)` +
`EnableSensitiveDataLogging()` in `Quotes.Tests.Integration/QueryTranslationTests.cs`.

## Whole-entity query vs. projection

Same filter (`Author == "Ada"`), two different result shapes.

| | Whole-entity (`ToList()` of `Quote`) | Projection (`Select(q => new QuoteDto {...})`) |
|---|---|---|
| SQL | `SELECT "q"."Id", "q"."Author", "q"."CreatedByUserId", "q"."Text"`<br>`FROM "Quotes" AS "q"`<br>`WHERE "q"."Author" = 'Ada'` | `SELECT "q"."Author", "q"."Text"`<br>`FROM "Quotes" AS "q"`<br>`WHERE "q"."Author" = 'Ada'` |

The whole-entity query pulls all four mapped columns — including `CreatedByUserId`, which
nothing in the test reads — because EF Core must materialize a complete, trackable `Quote`
instance. Projecting to a `QuoteDto { Author, Text }` tells EF the shape it actually needs up
front, so the generated `SELECT` list shrinks to exactly those two columns: less data over the
wire, less to deserialize, and (if tracked) nothing extra for the change tracker to snapshot.

## Client-side evaluation: caught, then fixed

**Failing expression** — a call to a local C# method inside the query predicate:

```csharp
private static bool IsShortAuthorName(string author) => author.Length < 8;
...
context.Quotes.Where(q => IsShortAuthorName(q.Author)).ToList();
```

**Exception** (`InvalidOperationException`, thrown before any SQL is sent):

```
The LINQ expression 'DbSet<Quote>()
    .Where(q => QueryTranslationTests.IsShortAuthorName(q.Author))' could not be translated.
Additional information: Translation of method
'Quotes.Tests.Integration.QueryTranslationTests.IsShortAuthorName' failed. If this method can
be mapped to your custom function, see https://go.microsoft.com/fwlink/?linkid=2132413 for more
information. Either rewrite the query in a form that can be translated, or switch to client
evaluation explicitly by inserting a call to 'AsEnumerable', 'AsAsyncEnumerable', 'ToList', or
'ToListAsync'. See https://go.microsoft.com/fwlink/?linkid=2101038 for more information.
```

EF Core has no SQL equivalent for an arbitrary C# method, so it refuses to guess — since EF Core
3.0 it throws immediately rather than silently evaluating the predicate client-side, which used
to mean pulling the whole table over just to filter it in memory.

**Fix 1 — rewrite in a translatable form**, expressing the same intent (`Author` shorter than 8
characters) with an expression EF already knows how to push to SQL:

```csharp
context.Quotes.Where(q => q.Author.Length < 8).ToList();
```

```sql
SELECT "q"."Id", "q"."Author", "q"."CreatedByUserId", "q"."Text"
FROM "Quotes" AS "q"
WHERE length("q"."Author") < 8
```

This is the preferred fix: the filter still runs in the database, so only the four matching rows
cross the wire.

**Fix 2 — explicit `AsEnumerable()`**, for cases where the predicate genuinely can't be expressed
in SQL (e.g. it truly needs arbitrary C# logic):

```csharp
context.Quotes.AsEnumerable().Where(q => IsShortAuthorName(q.Author)).ToList();
```

```sql
SELECT "q"."Id", "q"."Author", "q"."CreatedByUserId", "q"."Text"
FROM "Quotes" AS "q"
```

Here the `WHERE` clause disappears entirely — `AsEnumerable()` switches to LINQ-to-Objects before
the filter runs, so EF fetches every row and the local method filters them in memory. It returns
the same four rows as Fix 1, but only because the table is 304 rows; on a large table this is a
deliberate, visible trade-off instead of a silent one, which is the point of requiring it
explicitly.
