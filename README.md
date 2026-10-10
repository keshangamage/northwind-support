# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Implementation status

**Task 1 is implemented. Tasks 2, 3 and 4 are unfinished.**

### DEFECT-117: root cause and fix

The ticket-list effect depended only on search, page and page size, so changing another filter
did not fetch new results. Typing in search triggered the effect and sent the previously selected
filters, making the bug look intermittent.

The hook now watches the whole query. Search text is kept separately and enters the query after
300 ms without typing; dropdowns, sorting and pagination update the query immediately. Search and
filter changes reset the page to 1. Older responses cannot overwrite newer results.

To verify manually, open DevTools → Network and select **In progress**: a request containing
`status=InProgress` should appear immediately. Type several search characters: one request should
appear after typing stops. Clear search and change status again; it should still request results.

### Filtering and design decisions

- `TicketQueries.ApplyFilters` adds simple `Where` clauses to an `IQueryable`. The same filtered
  query is counted and then sorted, paged and projected by EF Core. No rows are filtered in memory.
- Status, priority, category, customer and agent are exact matches. Unassigned means a null agent.
  All supplied filters combine with AND, including contradictory filters that return no matches.
- Search is trimmed; blank text is ignored. It matches a substring of title, reference or customer
  name. Case sensitivity follows the database collation; the local SQL Server uses its default.
- A new filter needs a property in the API and frontend contracts, query-string serialization and
  one predicate in `ApplyFilters`. The hook does not need another dependency or special wiring.
- No new abstraction, database column, migration or index was needed. Reads remain `AsNoTracking`;
  existing indexes cover status/priority and customer/category/agent IDs. Substring search can still
  scan rows and would need a different approach at large scale.

### Testing and AI usage

`npm test` runs the backend and frontend tests. `npm run lint`, `npm run typecheck` and
`npm run build` check lint, TypeScript and both builds.

Frontend tests inspect actual fetch URLs, covering individual filters, debounce, clearing search,
resetting filters and responses arriving out of order. They failed on the original hook. Backend
tests execute real EF queries against an in-memory SQLite database, covering every exact filter,
combined filters, customer/title/reference search, blank search, empty results and filtered paging.
Captured SQL verifies that both the count and page queries contain `WHERE`, and the page uses
database paging. These tests do not require Docker.

The live SQL Server API was also checked through the web proxy for every filter, combined filters,
trimmed and case-insensitive search, empty results and paging. With the demo data, High priority
returned 14 tickets and 5 pages at a page size of 3. API logs confirmed `WHERE` in both the count
and page queries, followed by `ORDER BY` and `OFFSET ... FETCH NEXT` for the page.

OpenAI Codex was used to inspect the starter code, implement Task 1, write tests and run validation.
The simple EF `Where` approach was retained. A first fetch implementation failed React's lint rule
for setting state synchronously from an effect; it was revised to derive loading from the query
that produced the results. Changes were checked with tests that failed before the fixes, then
passed afterward, plus lint, types, builds and requests against local SQL Server.

### Known limitations

Automatic triage, escalation/history, the SLA filter, login/JWT and the API Docker image are not
implemented yet. The application currently has no authentication. Task 1 does not change those
features or introduce production deployment work.

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Database-filtered, paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
