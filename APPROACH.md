# Approach

## Task 1.1: filter request defect

The first check was the existing test suite, which passed on the starter code. The hook's effect
watched only search and pagination, so its tests did not cover changing a dropdown by itself.

Regression tests now use the real API client and mock only `fetch`. They assert the URL sent after
each filter change, rather than checking whether the displayed rows happen to look filtered. The
status regression failed on the original code because no request was sent.

The fix keeps the current request query in one state and the search input text in another. Only
search waits 300 ms before updating the query. All other filter changes update it immediately, and
the effect watches the whole query so a future filter does not need its own dependency. A request
number prevents an older response from replacing newer results. Loading is derived from whether
the current query has a completed response, avoiding a second loading-state update in the effect.

Tests also cover clearing search, clearing all filters, pagination, debounce and reversed response
order. A reload or remount was not used to hide the dependency bug.

## Task 1.2: database filtering

The existing query already joined the customer, category and optional agent, so those joins were
reused. `ApplyFilters` adds one `Where` clause per supplied filter. Chaining these clauses gives
AND behavior and leaves each condition easy to read and extend.

The filtered query is used for `CountAsync` before sorting and paging. Search is trimmed and
checks title, reference and customer name. No query is materialized until the final page is read.
Existing projections, stable sorting and `AsNoTracking` reads were preserved. The existing indexes
are sufficient for this task; no schema migration was added.

Tests run against SQLite, using deliberately different customers, categories, agents, priorities
and statuses. They verify exact matches, combined filters, search, conflicting filters and counts
across pages. SQL capture checks filtering on both queries and database paging. Local SQL Server
requests provide an additional check of the production database provider.

## Tasks 2, 3 and 4

Unfinished. No escalation, authentication or API-container implementation has been added.

## Time tracking

Record actual time spent on each task before submission. Setup time and implementation time
should be distinguished; no estimated or invented time has been filled in here.
