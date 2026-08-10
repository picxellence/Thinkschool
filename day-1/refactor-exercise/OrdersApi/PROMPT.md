Write a deliberately bad OrderController.cs for an ASP.NET Core 10 minimal API project. This is for a refactoring exercise, so intentionally include these anti-patterns:

- Roughly 300 lines total
- One giant POST /api/orders action that mixes business logic, EF Core data access, input validation, and HTTP response shaping all inline in a single method
- Four empty catch {} blocks that silently swallow exceptions
- Synchronous EF Core calls (like .ToList() or .SaveChanges()) inside a method marked async
- Return type of object instead of typed response classes
- Zero tests
- Include two subtle bugs: one off-by-one error (e.g. in a loop or index calculation) and one potential null reference exception
- Save it as OrderController.cs

Don't clean it up or add comments explaining the problems — I need it to look like real legacy code.
