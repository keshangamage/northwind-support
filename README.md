# Northwind Support Desk

A ticket management app built with .NET 10, React, TypeScript and SQL Server.

Tasks 1 and 2 are complete. Login/authentication and the API Docker image are unfinished.
Full requirements: [ASSIGNMENT_CANDIDATE.md](ASSIGNMENT_CANDIDATE.md).

## Run locally

Install .NET 10 SDK, Node.js 22 and Docker Desktop.

```bash
npm install
npm run db:up
docker compose ps
```

Wait for SQL Server to become healthy. On Apple Silicon, use
`DOCKER_DEFAULT_PLATFORM=linux/amd64 npm run db:up`.

Start the API in one terminal:

```bash
npm run api -- --launch-profile http
```

Start the web app in another:

```bash
VITE_API_PROXY_TARGET=http://localhost:5043 npm run web
```

Web: http://localhost:5173. API: http://localhost:5043. Swagger: http://localhost:5043/swagger.
The development API applies migrations and seeds 40 tickets on startup.
Local database credentials are `sa` / `Local_Dev_Password1`; overrides are in `.env.example`.
App login credentials are unavailable because authentication is unfinished.

## Ticket filtering

**DEFECT-117:** The effect watched only search and pagination, so other filters did not
send requests. It now watches the full query, debounces search for 300 ms and ignores older responses.

Filters combine with AND and run in SQL before counting, sorting and paging. Search is trimmed
and matches title, reference or customer name; blank search is ignored. Reads use `AsNoTracking`.
A new filter needs a contract field, query parameter and predicate. The SLA filter needed no hook changes.

To verify, change Status in DevTools Network: a request should appear immediately, including after
clearing search. Search sends a request after typing stops.

## Triage and escalation

- Use the requested priority, or Medium. Category flags can force Critical and require a specialist.
- SLA windows: Critical 4 h, High 8 h, Medium 24 h, Low 72 h. Premium customers get half, with a 1 h floor.
  Change `Sla` in `appsettings.Development.json` and restart the API to change these rules.
- Choose an active, qualified agent below their limit, using the fewest open tickets, then lowest ID.
  Open excludes Resolved and Closed. No eligible agent returns `201`, unassigned, with a reason.
  Manual assignment uses the same eligibility rules.
- Escalation raises priority one level and starts the due-date window from the injected clock.
  Keep an eligible owner; otherwise reassign. Capacity excludes the ticket being escalated.
- A trimmed reason needs 5–500 characters; the actor needs 1–100. Invalid input returns `400`,
  a missing ticket `404`, and Critical/Resolved/Closed tickets `409`.
- SLA status is derived: no due date means No SLA; resolved at or before due means Met;
  late resolution or open and past due means Breached. Open tickets with at most 25% remaining
  are At risk; otherwise Within SLA. The threshold is configurable. Exactly due is At risk.

Create shows the triage decision. Escalation validates input, shows errors and refreshes the ticket
and history without reloading the page. List and detail share one SLA indicator with text labels.

Endpoints: `POST /api/tickets`, `POST /api/tickets/{id}/escalate`,
`GET /api/tickets/{id}/escalations` and `GET /api/tickets?slaStatus=AtRisk`.
Escalation accepts `{ "reason": "Customer is blocked", "escalatedBy": "Team lead" }`.

## Design and database

Domain classes share triage, assignment and SLA rules between create and escalate.
Existing command/query handlers and repository interfaces keep database code separate.
Extra policy interfaces were unnecessary.

The `TicketTriageAndEscalations` migration adds history and `SlaWindowSeconds`.
Saving the window keeps SLA calculations correct after escalation. Windows and at-risk offsets
round up to whole seconds; existing windows are backfilled. SLA status itself is never stored.
One expression handles SQL status calculation, filtering and sorting.

Ticket changes and history save together. History rows have no update method, required reason/actor
lengths, optional agents/due dates and restrictive foreign keys. The ticket/time index supports
newest-first history; agent foreign-key indexes support relationship checks.

## Verification

```bash
npm test
npm run lint
npm run typecheck
npm run build
npm run format:check
```

All 138 tests and these checks passed. Tests cover actual filter requests, debounce, SQL count/paging,
SLA boundaries, assignment, escalation, history, client validation and server conflicts.
Database tests use SQLite and need no Docker. Live SQL Server checks also passed; logs confirmed
`WHERE`/`CASE` filtering and `OFFSET ... FETCH NEXT` paging. Manual UI checks passed.

## AI usage

Codex helped with ticket filtering, escalation and tests. I kept the simple EF filters and corrected
loading-state and configuration errors. Tests and live API checks verified the fixes.

## Limitations

Authentication and API containerisation are unfinished; escalation actors are entered manually.
Concurrent workload locking, paged history and browser end-to-end tests are not implemented.
Production needs safe reference allocation and capacity checks. Large-scale search may need a search index.
