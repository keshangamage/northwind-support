import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketDetail } from '../../../test/fixtures';
import type { EscalateTicketResult, TicketPriority, TicketStatus } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

const fetchMock = vi.fn<typeof fetch>();

function fillForm(reason = 'Customer is blocked') {
  fireEvent.change(screen.getByLabelText('Reason'), { target: { value: reason } });
  fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));
}

describe('EscalationPanel', () => {
  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => vi.unstubAllGlobals());

  it.each(['1234', '     ', 'a'.repeat(501)])('blocks an invalid reason before calling the API', (reason) => {
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);
    fillForm(reason);
    expect(screen.getByText('Reason must be between 5 and 500 characters.')).toBeVisible();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('shows a 409 and leaves the ticket unchanged', async () => {
    const ticket = ticketDetail();
    const onEscalated = vi.fn();
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ detail: 'Critical tickets cannot be escalated.' }), { status: 409 }));
    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);
    fillForm();
    expect(await screen.findByRole('alert')).toHaveTextContent('Critical tickets cannot be escalated.');
    expect(onEscalated).not.toHaveBeenCalled();
    expect(ticket.priority).toBe('High');
    expect(screen.getByLabelText('Reason')).toHaveValue('Customer is blocked');
    expect(screen.getByRole('button', { name: 'Escalate ticket' })).toBeEnabled();
  });

  it('shows pending state and returns the updated ticket and history on success', async () => {
    const onEscalated = vi.fn();
    const updated: EscalateTicketResult = {
      ticket: ticketDetail({ priority: 'Critical' }),
      escalation: {
        id: 1, ticketId: 12, fromPriority: 'High', toPriority: 'Critical',
        fromAgent: null, toAgent: null, fromDueAtUtc: null, toDueAtUtc: '2026-10-10T12:00:00Z',
        reason: 'Customer is blocked', escalatedBy: 'Team lead', escalatedAtUtc: '2026-10-10T08:00:00Z',
      },
    };
    let finish!: (response: Response) => void;
    fetchMock.mockImplementation(() => new Promise((resolve) => { finish = resolve; }));
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);
    fillForm('  Customer is blocked  ');
    expect(screen.getByRole('button', { name: 'Escalating...' })).toBeDisabled();
    expect(screen.getByLabelText('Reason')).toBeDisabled();
    expect(fetchMock).toHaveBeenCalledWith('/api/tickets/12/escalate', expect.objectContaining({
      method: 'POST', body: JSON.stringify({ reason: 'Customer is blocked' }),
    }));
    expect(screen.queryByLabelText('Escalated by')).not.toBeInTheDocument();
    finish(new Response(JSON.stringify(updated)));
    await waitFor(() => expect(onEscalated).toHaveBeenCalledWith(updated));
    expect(screen.getByLabelText('Reason')).toHaveValue('');
  });

  const blocked: [TicketPriority, TicketStatus][] = [['Critical', 'Open'], ['High', 'Resolved'], ['High', 'Closed']];
  it.each(blocked)('disables escalation for %s/%s', (priority, status) => {
    render(<EscalationPanel ticket={ticketDetail({ priority, status })} onEscalated={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Escalate ticket' })).toBeDisabled();
    expect(screen.queryByLabelText('Reason')).not.toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
