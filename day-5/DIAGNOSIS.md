# Diagnosis: N+1 query in `CollectionRepository.GetByIdAsync`

Demonstration endpoint: `POST /collections/{id}/items` (there is no `GET /collections/{id}`
in `Extensions/EndpointExtensions.cs` — only `POST /collections`, `POST /collections/{id}/items`,
and `DELETE /collections/{id}/items/{quoteId}`). The items endpoint calls the same
`GetByIdAsync` before appending the new item, so it exercises the bug directly.

Setup for both runs: fresh `quotes.db`, one collection seeded with exactly 20 items, then
one more `POST /collections/{id}/items` call to add a 21st item. That call is the one measured.

## Before

- **Trace id:** `51459461d3769eb51b82af759467939e`
- **Request duration:** 6.62 ms (top-level span `Activity.Duration`; Serilog's request-logging
  line independently recorded 6.5202 ms for the same request)
- **EF/DB spans:** 23 — 1 query for the collection (with its owned `Items` rows auto-included),
  **20 separate `SELECT` queries against `Quotes`, one per item**, 1 `INSERT` for the new
  `CollectionItem`, 1 `UPDATE` on `Collections` from `SaveChangesAsync`
- **Total spans in the trace:** 24 (23 EF spans + 1 top-level `POST /collections/{id:int}/items` span)

Representative span blocks from the console exporter (same `TraceId` throughout):

```
10:10:04 [DBG] [TraceId:51459461d3769eb51b82af759467939e] Executing DbCommand [Parameters=[@id='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SELECT "c1"."Id", "c1"."Name", "c1"."OwnerId", "c1"."OwnerUserId", "c0"."CollectionId", "c0"."QuoteId", "c0"."AddedAt"
FROM (
    SELECT "c"."Id", "c"."Name", "c"."OwnerId", "c"."OwnerUserId"
    FROM "Collections" AS "c"
    WHERE "c"."Id" = @id
    LIMIT 1
) AS "c1"
LEFT JOIN "CollectionItems" AS "c0" ON "c1"."Id" = "c0"."CollectionId"
ORDER BY "c1"."Id", "c0"."CollectionId"
Activity.TraceId:            51459461d3769eb51b82af759467939e
Activity.SpanId:             988f822c18b6a75c
Activity.ParentSpanId:       2c50c60a39b9811c
Activity.DisplayName:        main
Activity.Kind:               Client
Activity.Duration:           00:00:00.0001600
```

```
10:10:04 [DBG] [TraceId:51459461d3769eb51b82af759467939e] Executing DbCommand [Parameters=[@item_QuoteId='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SELECT "q"."Id", "q"."Author", "q"."CreatedByUserId", "q"."Text"
FROM "Quotes" AS "q"
WHERE "q"."Id" = @item_QuoteId
LIMIT 1
Activity.TraceId:            51459461d3769eb51b82af759467939e
Activity.SpanId:             8feeb515549a5b37
Activity.ParentSpanId:       2c50c60a39b9811c
Activity.DisplayName:        main
Activity.Kind:               Client
Activity.Duration:           00:00:00.0000870
```

```
10:10:04 [DBG] [TraceId:51459461d3769eb51b82af759467939e] Executing DbCommand [Parameters=[@item_QuoteId='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SELECT "q"."Id", "q"."Author", "q"."CreatedByUserId", "q"."Text"
FROM "Quotes" AS "q"
WHERE "q"."Id" = @item_QuoteId
LIMIT 1
Activity.TraceId:            51459461d3769eb51b82af759467939e
Activity.SpanId:             e1bb232f484a6db5
Activity.ParentSpanId:       2c50c60a39b9811c
Activity.DisplayName:        main
Activity.Kind:               Client
Activity.Duration:           00:00:00.0000480
```

The two blocks above are the 1st and 20th occurrence of the identical `SELECT "q"...WHERE
"q"."Id" = @item_QuoteId` query — same shape, same `TraceId`, 20 times in a row, one per
item in the collection.

```
10:10:04 [DBG] [TraceId:51459461d3769eb51b82af759467939e] Executing DbCommand [Parameters=[@p0='?' (DbType = Int32), @p1='?' (DbType = Int32), @p2='?' (DbType = DateTimeOffset)], CommandType='Text', CommandTimeout='30']
INSERT INTO "CollectionItems" ("CollectionId", "QuoteId", "AddedAt")
VALUES (@p0, @p1, @p2);
Activity.TraceId:            51459461d3769eb51b82af759467939e
Activity.SpanId:             1a1bcdaae1ca6886
Activity.ParentSpanId:       2c50c60a39b9811c
Activity.DisplayName:        main
Activity.Kind:               Client
Activity.Duration:           00:00:00.0000610
```

## After

- **Trace id:** `d5530fd559191364cafe8257f50a0545`
- **Request duration:** 2.63 ms (top-level span `Activity.Duration`; Serilog's request-logging
  line recorded 2.5115 ms for the same request)
- **EF/DB spans:** 3 — 1 query for the collection with `Items` included, 1 `INSERT` for the
  new `CollectionItem`, 1 `UPDATE` on `Collections` from `SaveChangesAsync`
- **Total spans in the trace:** 4 (3 EF spans + 1 top-level span)

Span blocks from the console exporter, same `TraceId` throughout:

```
10:15:55 [DBG] [TraceId:d5530fd559191364cafe8257f50a0545] Executing DbCommand [Parameters=[@id='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SELECT "c1"."Id", "c1"."Name", "c1"."OwnerId", "c1"."OwnerUserId", "c0"."CollectionId", "c0"."QuoteId", "c0"."AddedAt"
FROM (
    SELECT "c"."Id", "c"."Name", "c"."OwnerId", "c"."OwnerUserId"
    FROM "Collections" AS "c"
    WHERE "c"."Id" = @id
    LIMIT 1
) AS "c1"
LEFT JOIN "CollectionItems" AS "c0" ON "c1"."Id" = "c0"."CollectionId"
ORDER BY "c1"."Id", "c0"."CollectionId"
Activity.TraceId:            d5530fd559191364cafe8257f50a0545
Activity.SpanId:             48133ac0873514a0
Activity.ParentSpanId:       7daeb8c55d0f5a7d
Activity.DisplayName:        main
Activity.Kind:               Client
Activity.Duration:           00:00:00.0001920
```

```
10:15:55 [DBG] [TraceId:d5530fd559191364cafe8257f50a0545] Executing DbCommand [Parameters=[@p0='?' (DbType = Int32), @p1='?' (DbType = Int32), @p2='?' (DbType = DateTimeOffset)], CommandType='Text', CommandTimeout='30']
INSERT INTO "CollectionItems" ("CollectionId", "QuoteId", "AddedAt")
VALUES (@p0, @p1, @p2);
Activity.TraceId:            d5530fd559191364cafe8257f50a0545
Activity.SpanId:             75d6471b5b02f3f9
Activity.ParentSpanId:       7daeb8c55d0f5a7d
Activity.DisplayName:        main
Activity.Kind:               Client
Activity.Duration:           00:00:00.0001130
```

```
10:15:55 [INF] [TraceId:d5530fd559191364cafe8257f50a0545] HTTP POST /collections/1/items responded 200 in 2.5115 ms
Activity.TraceId:            d5530fd559191364cafe8257f50a0545
Activity.SpanId:             7daeb8c55d0f5a7d
Activity.DisplayName:        POST /collections/{id:int}/items
Activity.Kind:               Server
Activity.Duration:           00:00:00.0026290
```

No 500 error occurred with the fixed code — behavior is identical to before, only the query
count changed. (Note: the `SELECT "c1"...` query is unchanged before and after — `CollectionItem`
is mapped with `OwnsMany`, so EF already auto-includes owned rows on any query against
`Collections` regardless of `.Include`. The N+1 was entirely in the manual per-item `Quotes`
lookup loop, not in that first query.)

## Diagnosis note

This trace showed the slow span was the repeated `SELECT "q"."Id", "q"."Author",
"q"."CreatedByUserId", "q"."Text" FROM "Quotes" AS "q" WHERE "q"."Id" = @item_QuoteId` span,
appearing 20 times under trace `51459461d3769eb51b82af759467939e`, because
`CollectionRepository.GetByIdAsync` looped over every `CollectionItem` and issued a separate
round trip to fetch its `Quote` instead of loading them in the same query as the collection.
That pushed the request from 3 EF spans to 23 and the duration from 2.63 ms to 6.62 ms for a
20-item collection — on local SQLite; against a networked database the per-round-trip cost
would dominate far more. I'd fix it (and did) by replacing the manual load-then-loop with
`_context.Collections.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, ct)`, cutting
GetByIdAsync to a single query and the fetched-but-unused `Quote` entities were never even
referenced by the response, so nothing else needed to be included.
