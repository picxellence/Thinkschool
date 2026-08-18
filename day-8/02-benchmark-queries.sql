-- Day 8, step 2: the measurement set.
--
-- Run this file unchanged at every stage. The queries never change; only the
-- indexes underneath them do. Record the "logical reads" figure for each
-- query at each stage and the numbers tell the story.
--
-- Stages:
--   A  heap, no indexes
--   B  after the clustered index
--   C  after both non-clustered indexes
--   D  after the covering INCLUDE

USE IndexLab;
GO

SET STATISTICS IO ON;
SET STATISTICS TIME ON;
GO

PRINT '=== Q1: point lookup on the clustered key ===';
SELECT ViewId, QuoteId, UserId, ViewedAt, DurationMs
FROM   QuoteViews
WHERE  ViewId = 73512;
GO

PRINT '=== Q2: selective range - one user, recent views ===';
SELECT ViewId, ViewedAt, DurationMs
FROM   QuoteViews
WHERE  UserId = 1234
  AND  ViewedAt >= DATEADD(DAY, -30, SYSUTCDATETIME())
ORDER BY ViewedAt DESC;
GO

PRINT '=== Q3: aggregate over one quote - the covering-index candidate ===';
SELECT QuoteId,
       COUNT(*)        AS Views,
       AVG(DurationMs) AS AvgDurationMs
FROM   QuoteViews
WHERE  QuoteId = 42
GROUP BY QuoteId;
GO

PRINT '=== Q4: low-selectivity filter - the index that will not help ===';
SELECT COUNT(*)
FROM   QuoteViews
WHERE  Source = 'mobile';
GO

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
GO
