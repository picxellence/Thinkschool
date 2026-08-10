# REFACTOR_NOTES

## 1. Giant method

**Consequence:** Hard to understand, debug, and maintain.
**Fix:** Split into smaller methods and move logic to services.

## 2. Business logic in controller

**Consequence:** Controller has too many responsibilities.
**Fix:** Move business rules to OrderService.

## 3. EF Core access in controller

**Consequence:** Tight coupling to database layer.
**Fix:** Use repository or service abstraction.

## 4. Validation mixed with processing

**Consequence:** Validation is difficult to test and maintain.
**Fix:** Use model validation or FluentValidation.

## 5. Empty catch blocks

**Consequence:** Exceptions are silently swallowed.
**Fix:** Log and rethrow or remove unnecessary try/catch blocks.

## 6. Synchronous EF calls in async method

**Consequence:** Blocks threads and hurts scalability.
**Fix:** Replace with ToListAsync and SaveChangesAsync.

## 7. Return type is object

**Consequence:** API contract is unclear.
**Fix:** Return ActionResult<T> or typed DTOs.

## 8. Multiple SaveChanges calls

**Consequence:** Partial updates can be committed.
**Fix:** Use a transaction and save once when possible.

## 9. Off-by-one loop bug

**Consequence:** Can throw IndexOutOfRangeException.
**Fix:** Change i <= request.Items.Count to i < request.Items.Count.

## 10. Potential null reference

**Consequence:** Request may crash when Address or City is null.
**Fix:** Add null checks and required validation.

## 11. Magic strings

**Consequence:** Typos can create inconsistent states.
**Fix:** Use enums or constants.

## 12. Anonymous response objects

**Consequence:** Difficult to version and document.
**Fix:** Create response DTO classes.

## 13. No cancellation token

**Consequence:** Work continues after client disconnects.
**Fix:** Pass CancellationToken through all async operations.

## 14. No tests

**Consequence:** Refactoring becomes risky.
**Fix:** Add unit and integration tests.
