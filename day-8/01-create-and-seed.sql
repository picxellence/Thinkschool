-- Day 8, step 1: build a 100k-row heap with deliberately varied selectivity.
--
-- The table is created WITHOUT any index on purpose. Every measurement in
-- step 2 is taken against this heap first, so the later index numbers have
-- something to be compared against.

IF DB_ID('IndexLab') IS NULL
    CREATE DATABASE IndexLab;
GO

USE IndexLab;
GO

DROP TABLE IF EXISTS QuoteViews;
GO

CREATE TABLE QuoteViews (
    ViewId     BIGINT       NOT NULL,   -- 100,000 distinct  -> 1 row each
    QuoteId    INT          NOT NULL,   --   1,000 distinct  -> ~100 rows each
    UserId     INT          NOT NULL,   --   5,000 distinct  -> ~20 rows each
    ViewedAt   DATETIME2(0) NOT NULL,   -- spread over 90 days
    Source     VARCHAR(10)  NOT NULL,   --       3 distinct  -> ~33,000 rows each
    DurationMs INT          NOT NULL
);
GO

-- Generate 100,000 rows from a cross join of system tables. Faster and far
-- less log-heavy than a loop of single-row inserts.
WITH Numbers AS (
    SELECT TOP (100000)
           ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM   sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO QuoteViews (ViewId, QuoteId, UserId, ViewedAt, Source, DurationMs)
SELECT
    n,
    (ABS(CHECKSUM(NEWID())) % 1000) + 1,
    (ABS(CHECKSUM(NEWID())) % 5000) + 1,
    DATEADD(SECOND, -(ABS(CHECKSUM(NEWID())) % 7776000), SYSUTCDATETIME()),
    CASE (ABS(CHECKSUM(NEWID())) % 3)
        WHEN 0 THEN 'web'
        WHEN 1 THEN 'mobile'
        ELSE        'api'
    END,
    (ABS(CHECKSUM(NEWID())) % 2000) + 10
FROM Numbers;
GO

-- Confirm the selectivity spread is what the exercise assumes.
SELECT  COUNT(*)                  AS TotalRows,
        COUNT(DISTINCT ViewId)    AS DistinctViewId,
        COUNT(DISTINCT UserId)    AS DistinctUserId,
        COUNT(DISTINCT QuoteId)   AS DistinctQuoteId,
        COUNT(DISTINCT Source)    AS DistinctSource
FROM    QuoteViews;
GO
