# Northwind Support Desk

A ticket management app built with .NET 10, React, TypeScript and SQL Server.

Tasks 1–3 are complete. The API Docker image is unfinished.
Full requirements: [ASSIGNMENT_CANDIDATE.md](ASSIGNMENT_CANDIDATE.md).

## Run locally

Install .NET 10 SDK, Node.js 22 and Docker Desktop.

For a fresh checkout:

```bash
npm install
cp .env.example .env
openssl rand -hex 32
```

Paste the generated key into `Jwt__SigningKey` in your local `.env`. Then start SQL Server:

```bash
npm run db:up
docker compose ps
```

Wait for SQL Server to become healthy. On Apple Silicon, use
`DOCKER_DEFAULT_PLATFORM=linux/amd64 npm run db:up`.

Start the API in one terminal:

```bash
export Jwt__SigningKey="$(sed -n 's/^Jwt__SigningKey=//p' .env)"
npm run api -- --launch-profile http
```

Start the web app in another:

```bash
VITE_API_PROXY_TARGET=http://localhost:5043 npm run web
```

Web: http://localhost:5173. API: http://localhost:5043. Swagger: http://localhost:5043/swagger.
The development API applies migrations and seeds 40 tickets on startup.
Local database credentials are `sa` / `Local_Dev_Password1`; overrides are in `.env.example`.
Development login: `alex.turner@northwind-support.example` / `Test@1234`.
Other active seeded agents use the same local password. Inactive Yuki cannot sign in.

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
- A trimmed reason needs 5–500 characters; the actor comes from the signed-in agent. Invalid input returns `400`,
  a missing ticket `404`, and Critical/Resolved/Closed tickets `409`.
- SLA status is derived: no due date means No SLA; resolved at or before due means Met;
  late resolution or open and past due means Breached. Open tickets with at most 25% remaining
  are At risk; otherwise Within SLA. The threshold is configurable. Exactly due is At risk.

Create shows the triage decision. Escalation validates input, shows errors and refreshes the ticket
and history without reloading the page. List and detail share one SLA indicator with text labels.

Endpoints: `POST /api/tickets`, `POST /api/tickets/{id}/escalate`,
`GET /api/tickets/{id}/escalations` and `GET /api/tickets?slaStatus=AtRisk`.
Escalation accepts `{ "reason": "Customer is blocked" }`. Body-supplied actors are ignored.

## Authentication

The `AgentCredentials` migration adds a password hash to Agent. A separate user table was unnecessary
because only agents sign in. Identity `PasswordHasher` uses salted PBKDF2; development seeding hashes
missing credentials and keeps existing hashes.

`POST /api/auth/login` returns a JWT, expiry and agent details. Wrong email, wrong password and inactive
agents receive the same `401` message. `GET /api/auth/me` returns the current active agent.
A global fallback policy protects existing endpoints. Login and development Swagger stay open.
Tokens last 60 minutes; issuer, audience, signature and expiry are validated without clock grace.
Each machine generates its own random key and stores it in the ignored `.env` file. `.env.example`
contains an empty placeholder. The command above exports the key because `npm run api` does not load
`.env` automatically. Issuer, audience and lifetime are configured in `appsettings.json`.
Changing the key invalidates existing tokens.

The browser stores the token and agent details in `sessionStorage`, which survives reloads in the same
tab. The API client attaches the token to every call. Protected pages return visitors to their requested
page after login. Logout, expiry and any `401` clear the session. The header shows the signed-in name.
Escalation history uses the name from the JWT, limited to the history column's 100 characters.

This keeps the client simple, but JavaScript can read the token. Production should use HTTPS,
a secret store, HttpOnly cookies or managed identity, and token revocation.

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

All 165 tests and these checks passed. Tests cover actual filter requests, debounce, SQL count/paging,
SLA boundaries, assignment, escalation, history, client validation and server conflicts.
Database tests use SQLite and need no Docker. Live SQL Server checks also passed; logs confirmed
`WHERE`/`CASE` filtering and `OFFSET ... FETCH NEXT` paging. Manual UI checks passed.
Authentication tests use the real API with SQLite: generic login failures, protected endpoints,
invalid tokens, current user and token-based escalation actors. Frontend tests cover login validation,
return navigation, token headers, pending state, logout, expiry and `401` redirects.

## AI usage

Codex helped with filtering, authentication and tests. I kept the simple EF filters and corrected
loading-state and configuration errors. Tests and live API checks verified the fixes.

## Limitations

API containerisation is unfinished. JWTs cannot be revoked before expiry; refresh tokens, password
reset, MFA, lockout and rate limiting are outside the scope.
Concurrent workload locking, paged history and browser end-to-end tests are not implemented.
Production needs safe reference allocation and capacity checks. Large-scale search may need a search index.
