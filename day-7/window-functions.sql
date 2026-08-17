-- Per author, each quote in chronological order with:
--   a running count (ROW_NUMBER)
--   a running total (SUM ... OVER, the same value here, shown to contrast
--                    the ranking function with the aggregate window)
--   the days elapsed since that author's previous quote (LAG)
--   dense rank of the gap, longest first, within the author
--
-- PARTITION BY Author restarts every window at each new author.

SELECT
    Author,
    CreatedAt,
    Text,
    ROW_NUMBER() OVER (PARTITION BY Author ORDER BY CreatedAt)          AS QuoteNo,
    SUM(1)       OVER (PARTITION BY Author ORDER BY CreatedAt)          AS RunningTotal,
    LAG(CreatedAt) OVER (PARTITION BY Author ORDER BY CreatedAt)        AS PreviousQuoteAt,
    CAST(
        julianday(CreatedAt)
        - julianday(LAG(CreatedAt) OVER (PARTITION BY Author ORDER BY CreatedAt))
        AS INTEGER)                                                     AS DaysSincePrevious
FROM Quotes
ORDER BY Author, CreatedAt;