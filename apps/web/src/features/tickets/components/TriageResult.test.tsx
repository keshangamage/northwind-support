import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ticketDetail } from '../../../test/fixtures';
import { TriageResult } from './TriageResult';

describe('TriageResult', () => {
  it('shows the unassigned outcome and reasons', () => {
    render(<TriageResult ticket={ticketDetail({
      assignedAgent: null, priority: 'Critical', slaStatus: 'WithinSla',
      triage: {
        priorityReason: 'This category requires Critical priority.',
        slaReason: 'Premium customer: 2 hour SLA window.',
        assignmentReason: 'Unassigned because no active, qualified agent has capacity.',
      },
    })} />);
    expect(screen.getByText('Critical')).toBeVisible();
    expect(screen.getByText('Unassigned')).toBeVisible();
    expect(screen.getByText('Within SLA')).toBeVisible();
    expect(screen.getByText('Unassigned because no active, qualified agent has capacity.')).toBeVisible();
  });
});
