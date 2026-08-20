# Day 10 — EF Core change tracking measurements

SQLite, `Quote` table, 10,000 rows. Measured in `Quotes.Tests.Integration/ChangeTrackerTests.cs`,
each variant read on a fresh `QuotesDbContext` after a discarded warm-up `ToList()` on a
separate fresh context (to prime the query plan without polluting the identity map).

| Variant | Elapsed | Allocated |
|---|---|---|
| Tracked (default) | 46 ms | 9,831,520 bytes |
| `AsNoTracking()` | 12 ms | 3,867,968 bytes |

Allocation ratio (tracked / no-tracking): **2.54x**

Tracking the full 10k-row result set costs roughly 2.5x the allocations of `AsNoTracking()`,
because EF Core builds and maintains a change-tracker entry (snapshot, state, identity-map slot)
for every materialized `Quote` in addition to the entity instance itself. `AsNoTracking()` skips
that bookkeeping entirely, which also shows up as a lower wall-clock time in this run — though the
timing gap is more sensitive to machine noise than the allocation figures are.
