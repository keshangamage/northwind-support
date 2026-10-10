import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { page, ticketListItem } from '../../test/fixtures';
import { TicketListPage } from './TicketListPage';

describe('TicketListPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('sends the selected SLA filter and clears it on reset', async () => {
    const fetchMock = vi.fn<typeof fetch>(async (input) =>
      new Response(JSON.stringify(String(input).includes('/tickets?') ? page([ticketListItem()]) : [])));
    vi.stubGlobal('fetch', fetchMock);
    render(<MemoryRouter><TicketListPage /></MemoryRouter>);
    expect(await screen.findByRole('link', { name: 'TCK-0012' })).toBeVisible();
    fetchMock.mockClear();
    fireEvent.change(screen.getByLabelText('SLA status'), { target: { value: 'AtRisk' } });
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('slaStatus=AtRisk'), expect.any(Object)));
    fetchMock.mockClear();
    fireEvent.click(screen.getByRole('button', { name: 'Clear filters' }));
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(String(fetchMock.mock.calls[0][0])).not.toContain('slaStatus=');
    expect(screen.getByLabelText('SLA status')).toHaveValue('');
  });
});
