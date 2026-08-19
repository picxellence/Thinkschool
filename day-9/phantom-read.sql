-- PHANTOM READ: B runs the same range query twice and a new row appears,
-- because A inserted into that range in between.
-- Run interleaved across two sessions in the order shown.

-- [B] 1. open a transaction and count a range
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRANSACTION;
SELECT COUNT(*) FROM Accounts WHERE Balance BETWEEN 100 AND 400;   -- 2

-- [A] 2. insert a new row inside that range and commit
INSERT INTO Accounts VALUES (5, 'Eve', 250);

-- [B] 3. re-count in the SAME transaction -> one more row
SELECT COUNT(*) FROM Accounts WHERE Balance BETWEEN 100 AND 400;   -- 3
COMMIT;

-- PREVENTION: B at SERIALIZABLE takes a range lock, so A's insert blocks
-- until B commits and the count stays stable.
