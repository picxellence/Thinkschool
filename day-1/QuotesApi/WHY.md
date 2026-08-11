[# WHY.md — Anemic to Rich: What Changed and Why

The anemic Quote model was just three public, mutable properties. Nothing stopped invalid data from
entering the system — any code, anywhere, could do `quote.Text = ""` or assign a 5000-character
string, and the compiler would never complain. Validation lived only in the endpoint, meaning if a
second code path ever created a Quote (a background job, an admin tool, a future feature), that
validation would need to be duplicated or it would silently be skipped.

The rich model moves validation into the entity itself. `Quote.Create(author, text)` is now the only
way to construct a Quote, and it enforces the same rules (1-200 char author, 1-1000 char text) no
matter who calls it. Private setters mean once created, Text can never be reassigned directly — only
soft-deleted via `IsDeleted`, preserving the record instead of destroying it.

Concrete bug the anemic version would have shipped: before this refactor, nothing prevented posting
a quote with a 5000-character text field directly to the database if a future endpoint or import
script bypassed the controller's manual checks. I tested this directly — sending a 1001-character
quote now correctly returns a 400 with "Text must be 1-1000 characters," instead of silently
succeeding and corrupting downstream displays or storage limits.]
