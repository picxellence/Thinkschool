-- DIRTY READ: B reads data A never committed.
-- Run interleaved across two sessions in the order shown.

-- [A] 1. open a transaction, change a row, do NOT commit
BEGIN TRANSACTION;
UPDATE Accounts SET Balance = 999 WHERE Id = 1;

-- [B] 2. read at the lowest level -> sees 999, the uncommitted value
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SELECT Balance FROM Accounts WHERE Id = 1;   -- 999

-- [A] 3. roll back -> the 999 never existed
ROLLBACK;

-- PREVENTION: B at READ COMMITTED blocks on step 2 until A ends,
-- then reads the true committed value.
