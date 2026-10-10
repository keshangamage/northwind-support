import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { page, ticketListItem } from '../../../test/fixtures';
import type { TicketFilters } from '../../../types/api';
import { useTicketList } from './useTicketList';

const fetchMock = vi.fn<typeof fetch>();

function ticketResponse(title = 'A ticket') {
  return new Response(JSON.stringify(page([ticketListItem({ title })])));
}

function lastQuery() {
  const request = fetchMock.mock.calls.at(-1);
  if (!request) throw new Error('No request was sent.');
  return new URL(String(request[0]), 'http://localhost').searchParams;
}

const filterChanges: [string, Partial<TicketFilters>, string, string][] = [
  ['status', { status: 'InProgress' }, 'status', 'InProgress'],
  ['priority', { priority: 'High' }, 'priority', 'High'],
  ['SLA status', { slaStatus: 'AtRisk' }, 'slaStatus', 'AtRisk'],
  ['category', { categoryId: 2 }, 'categoryId', '2'],
  ['customer', { customerId: 3 }, 'customerId', '3'],
  ['agent', { assignedAgentId: 4 }, 'assignedAgentId', '4'],
  ['unassigned', { unassignedOnly: true }, 'unassignedOnly', 'true'],
  ['sort field', { sortBy: 'priority' }, 'sortBy', 'priority'],
  ['sort direction', { sortDirection: 'asc' }, 'sortDirection', 'asc'],
  ['page', { page: 2 }, 'page', '2'],
  ['page size', { pageSize: 10 }, 'pageSize', '10'],
];

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    fetchMock.mockReset().mockImplementation(async () => ticketResponse());
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it.each(filterChanges)('requests immediately when %s changes', async (_, patch, key, value) => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    fetchMock.mockClear();

    await act(async () => {
      result.current.updateFilters(patch);
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(lastQuery().get(key)).toBe(value);
    expect(lastQuery().get('page')).toBe(patch.page === undefined ? '1' : String(patch.page));
  });

  it('waits until typing settles and resets the page without requesting per keystroke', async () => {
    const { result } = renderHook(() => useTicketList());
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    await act(async () => {
      result.current.updateFilters({ page: 3 });
      await vi.advanceTimersByTimeAsync(300);
    });
    fetchMock.mockClear();

    await act(async () => {
      result.current.updateFilters({ search: 'inv' });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });
    await act(async () => {
      result.current.updateFilters({ search: 'invoice' });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(299);
    });

    expect(result.current.filters.search).toBe('invoice');
    expect(fetchMock).not.toHaveBeenCalled();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(lastQuery().get('search')).toBe('invoice');
    expect(lastQuery().get('page')).toBe('1');
    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  it('still requests a status change after the search is cleared', async () => {
    const { result } = renderHook(() => useTicketList());
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    await act(async () => {
      result.current.updateFilters({ search: 'invoice' });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    await act(async () => {
      result.current.updateFilters({ search: '' });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    fetchMock.mockClear();

    await act(async () => {
      result.current.updateFilters({ status: 'InProgress' });
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(lastQuery().get('status')).toBe('InProgress');
    expect(lastQuery().has('search')).toBe(false);
  });

  it('clears filters immediately and cancels pending search text', async () => {
    const { result } = renderHook(() => useTicketList());
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    await act(async () => {
      result.current.updateFilters({ status: 'InProgress' });
      await vi.advanceTimersByTimeAsync(300);
    });
    await act(async () => {
      result.current.updateFilters({ search: 'invoice' });
    });
    fetchMock.mockClear();

    await act(async () => {
      result.current.resetFilters();
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(lastQuery().has('status')).toBe(false);
    expect(lastQuery().has('search')).toBe(false);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('keeps the latest results when an earlier request finishes later', async () => {
    const { result } = renderHook(() => useTicketList());
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    let finishEarlier!: (response: Response) => void;
    fetchMock.mockImplementationOnce(() => new Promise((resolve) => {
      finishEarlier = resolve;
    }));
    await act(async () => {
      result.current.updateFilters({ status: 'Open' });
    });
    fetchMock.mockResolvedValueOnce(ticketResponse('Latest result'));
    await act(async () => {
      result.current.updateFilters({ status: 'InProgress' });
    });
    await act(async () => {
      finishEarlier(ticketResponse('Old result'));
    });

    expect(result.current.data?.items[0].title).toBe('Latest result');
  });
});
