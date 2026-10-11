# Approach

## Task 1: filtering

The request effect missed filter dependencies. I changed it to watch the full query and debounce
only search. Tests inspect actual fetch URLs and failed before the fix. Older responses are ignored.

Backend filters use simple `Where` clauses before count and paging. Existing joins and indexes were
reused. This made the SLA filter easy to add without changing the list hook.

## Task 2: triage and escalation

I kept the rules in three small domain classes: `SlaPolicy`, `TicketAssignment` and `TicketTriage`.
Create and escalate share them. Repositories load data, and the clock makes time-based rules testable.

Assignment uses open-ticket counts and category flags, with agent ID breaking ties. Escalation
excludes its own ticket from capacity, records old/new values and saves history with the ticket.

The saved SLA window stays correct after escalation. A shared expression calculates status in SQL,
so filtering, sorting and counts agree. The migration adds history and backfills existing windows.

The UI reuses the SLA indicator, validates escalation input and refreshes history after success.
Tests cover boundaries, assignment, database queries, errors and page updates. SQL Server and manual
UI checks confirmed the behavior.

## Task 3: authentication

I added a password hash to Agent and used Identity's password hasher. The development seeder fills
missing credentials without resetting existing ones. JWT settings and the signing key come from
configuration; one fallback policy protects the API. The random signing key stays in the ignored
local `.env`, with an empty placeholder in `.env.example`.

The login page and route guard share a small session store. The API client adds the token and clears
it on `401`; logout and expiry do the same. Escalation actors come from token claims.
Tests check login failures, protected endpoints, invalid tokens, actor spoofing and browser sessions.

## Remaining work

Task 4 is unfinished. See README.md for limitations and setup.
Record actual setup and task time before submission.
