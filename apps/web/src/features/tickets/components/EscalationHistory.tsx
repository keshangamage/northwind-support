import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { TicketEscalation } from '../../../types/api';

interface EscalationHistoryProps {
  entries: TicketEscalation[];
  isLoading: boolean;
  error?: string;
  onRetry: () => void;
}

export function EscalationHistory({ entries, isLoading, error, onRetry }: EscalationHistoryProps) {
  return (
    <section className="card">
      <h2>Escalation history</h2>
      {isLoading && <Spinner label="Loading escalation history" />}
      {error && <ErrorMessage message={error} onRetry={onRetry} />}
      {!isLoading && !error && entries.length === 0 && <p>No escalations yet.</p>}
      <ol className="escalation-history">
        {entries.map((entry) => (
          <li key={entry.id}>
            <p><strong>{entry.fromPriority} → {entry.toPriority}</strong></p>
            <p>Agent: {entry.fromAgent?.fullName ?? 'Unassigned'} → {entry.toAgent?.fullName ?? 'Unassigned'}</p>
            <p>Due: {formatDateTime(entry.fromDueAtUtc)} → {formatDateTime(entry.toDueAtUtc)}</p>
            <p className="description">{entry.reason}</p>
            <p className="summary__note">{entry.escalatedBy} · {formatDateTime(entry.escalatedAtUtc)}</p>
          </li>
        ))}
      </ol>
    </section>
  );
}
