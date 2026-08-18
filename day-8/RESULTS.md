# Day 8 — Index measurement results

SQL Server 2022, QuoteViews table, 100,000 rows.
Logical reads from SET STATISTICS IO ON.

| Query | A: heap | B: clustered | C: +non-clustered | D: +covering |
|---|---|---|---|---|
| Q1 `ViewId = 73512` | 529 | 3 | 3 | 3 |
| Q2 `UserId` + 30-day range | 529 | 565 | 33 | 33 |
| Q3 aggregate on `QuoteId` | 529 | 565 | 347 | 2 |
| Q4 `Source = 'mobile'` | 529 | 565 | 565 | 565 |

## Index storage

| Index | Type | Size |
|---|---|---|
| CIX_QuoteViews_ViewId | CLUSTERED | 4.82 MB |
| IX_QuoteViews_QuoteId_Incl | NONCLUSTERED | 2.63 MB |
| IX_QuoteViews_UserId_ViewedAt | NONCLUSTERED | 2.82 MB |

## Task 2 — Covering index + included columns

### Query

```sql
SELECT QuoteId,
       COUNT(*) AS Views,
       AVG(DurationMs) AS AvgDurationMs
FROM QuoteViews
WHERE QuoteId = 42
GROUP BY QuoteId;
