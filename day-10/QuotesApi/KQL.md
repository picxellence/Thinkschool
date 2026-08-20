# KQL queries for Application Insights

`operation_Id` in these tables holds the same W3C trace id we return as the `X-Trace-Id`
response header (see `CorrelationIdMiddleware`). A customer quoting that header value
leads straight to their request with the first query below.

## Slowest 10 requests in the last hour

```kql
// Which requests were slowest in the last hour, worth investigating first?
requests
| where timestamp > ago(1h)
| top 10 by duration desc
| project timestamp, name, duration, resultCode, operation_Id
```

## Everything for a single operation_Id (== X-Trace-Id), in order

```kql
// Given one operation_Id (a customer's X-Trace-Id), what's the full story of that
// request - every log line, every dependency call, and the request itself?
let targetOperationId = "00000000000000000000000000000000"; // paste the X-Trace-Id / operation_Id here
union requests, dependencies, traces
| where operation_Id == targetOperationId
| project timestamp, itemType, name, message, duration, resultCode, severityLevel
| order by timestamp asc
```

## Alert condition: average duration of POST /api/quotes over the last 5 minutes

```kql
// Should the "slow quote creation" alert be firing right now?
requests
| where timestamp > ago(5m)
| where name == "POST /api/quotes"
| summarize avgDuration = avg(duration)
```

## Requests issuing an abnormal number of dependency (DB) calls — N+1 finder

```kql
// Detects the N+1 signature: one request, many near-identical dependency calls sharing
// its operation_Id. Groups dependencies by operation_Id, counts them, and surfaces any
// request that made more than 10 database calls to reach its result.
dependencies
| where timestamp > ago(1h)
| where type in ("SQL", "SQLite")
| summarize dependencyCount = count(), sampleTarget = any(target) by operation_Id
| where dependencyCount > 10
| join kind=inner (
    requests
    | project operation_Id, name, requestDuration = duration, resultCode
) on operation_Id
| project operation_Id, name, requestDuration, dependencyCount, sampleTarget, resultCode
| order by dependencyCount desc
```
