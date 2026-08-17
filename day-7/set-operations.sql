-- Q1. Authors who have quotes but no tags.
-- EXCEPT: every author, minus those that appear in the tagged set.
SELECT DISTINCT Author FROM Quotes
EXCEPT
SELECT DISTINCT q.Author
FROM   Quotes q
JOIN   QuoteTags qt ON qt.QuoteId = q.Id;


-- Q2. Authors appearing in both the 'classic' and 'modern' tag sets.
-- INTERSECT: rows present in both result sets.
SELECT DISTINCT q.Author
FROM   Quotes q
JOIN   QuoteTags qt ON qt.QuoteId = q.Id
JOIN   Tags t       ON t.Id = qt.TagId
WHERE  t.Category = 'classic'
INTERSECT
SELECT DISTINCT q.Author
FROM   Quotes q
JOIN   QuoteTags qt ON qt.QuoteId = q.Id
JOIN   Tags t       ON t.Id = qt.TagId
WHERE  t.Category = 'modern';


-- Q3. The combined distinct tag list across both categories.
-- UNION (not UNION ALL): de-duplicates, which is what "distinct" asks for.
SELECT Name FROM Tags WHERE Category = 'classic'
UNION
SELECT Name FROM Tags WHERE Category = 'modern'
ORDER BY Name;