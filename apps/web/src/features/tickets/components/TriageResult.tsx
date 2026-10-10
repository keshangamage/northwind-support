import { formatDateTime } from '../../../lib/format';
import type { TicketDetail } from '../../../types/api';
import { SlaCell } from './SlaCell';

export function TriageResult({ ticket }: { ticket: TicketDetail }) {
  return (
    <div>
      <h2>Triage result</h2>
      <p>Priority: <strong>{ticket.priority}</strong></p>
      <p>Assigned agent: <strong>{ticket.assignedAgent?.fullName ?? 'Unassigned'}</strong></p>
      <p>Due: {formatDateTime(ticket.dueAtUtc)}</p>
      <SlaCell dueAtUtc={ticket.dueAtUtc} slaStatus={ticket.slaStatus} />
      {ticket.triage && (
        <>
          <p>{ticket.triage.priorityReason}</p>
          <p>{ticket.triage.slaReason}</p>
          <p>{ticket.triage.assignmentReason}</p>
        </>
      )}
    </div>
  );
}
