-- Day 9: Deadlock fix
-- Both transactions acquire locks in the same order: Id 1 -> Id 2.

-- SESSION A
BEGIN TRANSACTION;

UPDATE Accounts
SET Balance = Balance - 10
WHERE Id = 1;

UPDATE Accounts
SET Balance = Balance + 10
WHERE Id = 2;

COMMIT;
GO


-- SESSION B
BEGIN TRANSACTION;

UPDATE Accounts
SET Balance = Balance + 10
WHERE Id = 1;

UPDATE Accounts
SET Balance = Balance - 10
WHERE Id = 2;

COMMIT;
GO
