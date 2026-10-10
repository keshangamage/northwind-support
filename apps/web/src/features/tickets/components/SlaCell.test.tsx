import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaCell } from './SlaCell';

const states: [SlaStatus, string][] = [
  ['WithinSla', 'Within SLA'], ['AtRisk', 'At risk'], ['Breached', 'Breached'],
  ['Met', 'Met'], ['NotApplicable', 'No SLA'],
];

describe('SlaCell', () => {
  it.each(states)('shows %s with a distinct text label', (slaStatus, label) => {
    render(<SlaCell dueAtUtc={null} slaStatus={slaStatus} />);
    expect(screen.getByText(label)).toBeVisible();
  });
});
