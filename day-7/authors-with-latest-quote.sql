-- Each author with their quote count and their most recent quote,
-- in one statement, using CTEs rather than correlated subqueries.
--
-- Note: the Quotes table has no created timestamp, so "most recent" is
-- defined by Id DESC — valid because Id is an autoincrement key, but a
-- real schema should carry a CreatedAt.

WITH AuthorStats AS (
    SELECT  Author,
            COUNT(*) AS QuoteCount
    FROM    Quotes
    GROUP BY Author
),
RankedQuotes AS (
    SELECT  Id,
            Author,
            Text,
            ROW_NUMBER() OVER (PARTITION BY Author ORDER BY Id DESC) AS Rn
    FROM    Quotes
)
SELECT  s.Author,
        s.QuoteCount,
        r.Text AS MostRecentQuote
FROM    AuthorStats  AS s
JOIN    RankedQuotes AS r
        ON  r.Author = s.Author
        AND r.Rn = 1
ORDER BY s.QuoteCount DESC, s.Author
LIMIT 10;