import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ticketDetail } from '../../test/fixtures';
import type { TicketEscalation } from '../../types/api';
import { TicketDetailPage } from './TicketDetailPage';

describe('TicketDetailPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('refreshes the ticket and history after escalation', async () => {
    const original = ticketDetail({ priority: 'Medium' });
    const updated = ticketDetail({ priority: 'High' });
    const history: TicketEscalation[] = [];
    const escalation: TicketEscalation = {
      id: 1, ticketId: 12, fromPriority: 'Medium', toPriority: 'High',
      fromAgent: original.assignedAgent, toAgent: updated.assignedAgent,
      fromDueAtUtc: original.dueAtUtc, toDueAtUtc: updated.dueAtUtc,
      reason: 'Customer is blocked', escalatedBy: 'Team lead', escalatedAtUtc: '2026-10-10T08:00:00Z',
    };
    const fetchMock = vi.fn<typeof fetch>(async (input, init) => {
      const path = String(input);
      if (path.endsWith('/escalate') && init?.method === 'POST') {
        history.push(escalation);
        return new Response(JSON.stringify({ ticket: updated, escalation }));
      }
      if (path.endsWith('/escalations')) return new Response(JSON.stringify(history));
      if (path.endsWith('/tickets/12')) return new Response(JSON.stringify(original));
      return new Response('[]');
    });
    vi.stubGlobal('fetch', fetchMock);
    render(
      <MemoryRouter initialEntries={['/tickets/12']}>
        <Routes><Route path="/tickets/:id" element={<TicketDetailPage />} /></Routes>
      </MemoryRouter>,
    );
    expect(await screen.findByText('Medium')).toBeVisible();
    expect(await screen.findByText('No escalations yet.')).toBeVisible();
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Customer is blocked' } });
    fireEvent.change(screen.getByLabelText('Escalated by'), { target: { value: 'Team lead' } });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));
    expect(await screen.findByText('Medium → High')).toBeVisible();
    expect(screen.getByText('High')).toBeVisible();
    expect(screen.getByText('Customer is blocked')).toBeVisible();
    await waitFor(() => expect(fetchMock.mock.calls.filter(([input]) =>
      String(input).endsWith('/escalations'))).toHaveLength(2));
  });
});
