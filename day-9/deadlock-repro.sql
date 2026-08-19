-- Day 9: Deadlock reproduction
-- Run the two sessions separately.

-- SESSION A
BEGIN TRANSACTION;

-- Lock Id = 1 first
UPDATE Accounts
SET Balance = Balance - 10
WHERE Id = 1;

-- Then request Id = 2
UPDATE Accounts
SET Balance = Balance + 10
WHERE Id = 2;

-- Do not COMMIT while reproducing the deadlock.


-- SESSION B
BEGIN TRANSACTION;

-- Lock Id = 2 first
UPDATE Accounts
SET Balance = Balance - 10
WHERE Id = 2;

-- Then request Id = 1
UPDATE Accounts
SET Balance = Balance + 10
WHERE Id = 1;

-- Do not COMMIT while reproducing the deadlock.
