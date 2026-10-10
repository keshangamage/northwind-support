import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  query?: TicketFilters;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [query, setQuery] = useState<TicketFilters>(emptyFilters);
  const [search, setSearch] = useState('');
  const [state, setState] = useState<State>({});
  const requestId = useRef(0);

  // Wait for typing to stop.
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setQuery((current) => {
        if (current.search === search) return current;
        return { ...current, search, page: 1 };
      });
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(timer);
  }, [search]);

  const load = useCallback(() => {
    const id = ++requestId.current;

    return ticketsApi.getTickets(query)
      .then((data) => {
        if (id === requestId.current) {
          setState({ query, data });
        }
      })
      .catch((error: unknown) => {
        if (id === requestId.current) {
          const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
          setState((current) => ({ ...current, query, error: message }));
        }
      });
  }, [query]);

  useEffect(() => {
    const requests = requestId;
    void load();

    // Ignore old responses.
    return () => {
      requests.current++;
    };
  }, [load]);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    const { search: nextSearch, ...otherFilters } = patch;
    if (nextSearch !== undefined) setSearch(nextSearch);

    if (Object.keys(otherFilters).length > 0) {
      setQuery((current) => ({ ...current, ...otherFilters, page: otherFilters.page ?? 1 }));
    }
  };

  const resetFilters = () => {
    setSearch('');
    setQuery(emptyFilters);
  };

  const reload = () => {
    setState((current) => ({ ...current, query: undefined, error: undefined }));
    return load();
  };

  return {
    filters: { ...query, search },
    updateFilters,
    resetFilters,
    reload,
    isLoading: state.query !== query,
    data: state.data,
    error: state.query === query ? state.error : undefined,
  };
}
