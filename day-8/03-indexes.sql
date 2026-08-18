-- Day 8, step 3: the indexes, applied one stage at a time.
--
-- Run each section separately and re-run 02-benchmark-queries.sql in between,
-- so each change can be attributed to a specific index.

USE IndexLab;
GO

--------------------------------------------------------------------------
-- STAGE B: the clustered index
--------------------------------------------------------------------------
-- A clustered index is not "an index on the table" — it IS the table,
-- reordered. The leaf level holds the actual rows, sorted by ViewId. There
-- can only be one, which is why the choice of key matters more than any
-- other index decision.
--
-- ViewId is the right key here: unique, narrow, ever-increasing. Increasing
-- keys append at the end rather than inserting into the middle, which avoids
-- page splits on write.

CREATE CLUSTERED INDEX CIX_QuoteViews_ViewId
    ON QuoteViews (ViewId);
GO

--------------------------------------------------------------------------
-- STAGE C: two non-clustered indexes
--------------------------------------------------------------------------
-- A non-clustered index is a separate structure holding the key columns plus
-- a pointer back to the clustered index. Following that pointer is a "key
-- lookup", and it is the cost that Stage D removes.

-- Composite. Column order matters: UserId first because it is the equality
-- predicate, ViewedAt second because it is the range. Reversed, the index
-- would be near-useless for Q2.
CREATE NONCLUSTERED INDEX IX_QuoteViews_UserId_ViewedAt
    ON QuoteViews (UserId, ViewedAt);
GO

-- Deliberately NOT covering yet. Q3 will seek this index and then perform a
-- key lookup per row to fetch DurationMs. That lookup is visible in the plan
-- and in the read counts.
CREATE NONCLUSTERED INDEX IX_QuoteViews_QuoteId
    ON QuoteViews (QuoteId);
GO

--------------------------------------------------------------------------
-- STAGE D: make the second index covering
--------------------------------------------------------------------------
-- INCLUDE stores DurationMs at the leaf level without making it part of the
-- key. The index can now answer Q3 by itself, so the key lookup disappears.
-- Compare Q3's logical reads before and after this statement.

DROP INDEX IX_QuoteViews_QuoteId ON QuoteViews;
GO

CREATE NONCLUSTERED INDEX IX_QuoteViews_QuoteId_Incl
    ON QuoteViews (QuoteId)
    INCLUDE (DurationMs);
GO

--------------------------------------------------------------------------
-- Inspect what now exists, and what it costs to store
--------------------------------------------------------------------------
SELECT  i.name          AS IndexName,
        i.type_desc     AS IndexType,
        SUM(p.rows)     AS Rows,
        SUM(au.total_pages) * 8 / 1024.0 AS SizeMB
FROM    sys.indexes i
JOIN    sys.partitions p       ON p.object_id = i.object_id AND p.index_id = i.index_id
JOIN    sys.allocation_units au ON au.container_id = p.partition_id
WHERE   i.object_id = OBJECT_ID('QuoteViews')
GROUP BY i.name, i.type_desc
ORDER BY i.type_desc, i.name;
GO
