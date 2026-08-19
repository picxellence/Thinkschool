-- NON-REPEATABLE READ: B reads the same row twice and gets two answers,
-- because A committed a change in between.
-- Run interleaved across two sessions in the order shown.

-- [B] 1. open a transaction and read the row
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRANSACTION;
SELECT Balance FROM Accounts WHERE Id = 2;   -- 200

-- [A] 2. update the same row and commit
UPDATE Accounts SET Balance = 500 WHERE Id = 2;
COMMIT;

-- [B] 3. read again in the SAME transaction -> different value
SELECT Balance FROM Accounts WHERE Id = 2;   -- 500
COMMIT;

-- PREVENTION: B at REPEATABLE READ locks the row on the first read,
-- so A's update blocks until B commits and both reads match.
