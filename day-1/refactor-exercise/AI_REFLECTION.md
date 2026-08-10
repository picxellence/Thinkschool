# AI_REFLECTION

For the strategy-pattern refactor, Claude was most helpful in identifying the tax-calculation logic as the messiest part of the code. Extracting country-specific tax rules into separate strategy classes made the service easier to extend without modifying the main order workflow. I would have reviewed the provider-selection logic carefully because that is the area where a wrong default strategy could silently produce incorrect tax values.

Copilot was not available for inline test generation in my environment, so I wrote the tests manually. The useful part of the workflow was thinking about behavior-first tests: US tax, UK tax, default tax, and strategy selection. Those tests gave confidence that the refactor preserved the business rules.

A subtle risk in the AI-generated refactor is over-engineering. For a small number of tax rules, introducing too many abstractions could make the code harder to navigate than the original implementation. I would also verify that new countries can be added through dependency injection without changing existing tests.

If I were debugging a production issue at 2 AM, I would reach for Claude first to analyze the overall structure and identify suspicious areas quickly. For small repetitive edits and test scaffolding, Copilot would be the faster tool.
