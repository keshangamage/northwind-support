import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { EscalateTicketResult, TicketDetail } from '../../../types/api';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (result: EscalateTicketResult) => void;
}

export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string>();
  const [isSaving, setIsSaving] = useState(false);
  const isFinished = ticket.status === 'Resolved' || ticket.status === 'Closed';
  const isDisabled = isFinished || ticket.priority === 'Critical';

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (isSaving || isDisabled) return;

    const validation: Record<string, string> = {};
    if (reason.trim().length < 5 || reason.trim().length > 500) {
      validation.reason = 'Reason must be between 5 and 500 characters.';
    }
    setErrors(validation);
    setSubmitError(undefined);
    if (Object.keys(validation).length > 0) return;

    setIsSaving(true);
    try {
      const result = await ticketsApi.escalateTicket(ticket.id, {
        reason: reason.trim(),
      });
      setReason('');
      onEscalated(result);
    } catch (error) {
      if (error instanceof ApiError) {
        setErrors(Object.fromEntries(
          Object.entries(error.fieldErrors).map(([key, messages]) => [
            key.charAt(0).toLowerCase() + key.slice(1), messages[0],
          ]),
        ));
      }
      setSubmitError(toErrorMessage(error, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <form className="card form" onSubmit={submit} noValidate>
      <h2>Escalate ticket</h2>
      {isDisabled && (
        <p>{isFinished ? `${ticket.status} tickets` : 'Critical tickets'} cannot be escalated.</p>
      )}
      {!isDisabled && (
        <>
          <Field id="escalation-reason" label="Reason" hint="5–500 characters." error={errors.reason}>
            {(props) => (
              <textarea
                {...props}
                rows={3}
                maxLength={500}
                value={reason}
                disabled={isSaving}
                onChange={(event) => setReason(event.target.value)}
              />
            )}
          </Field>
        </>
      )}
      {submitError && (
        <p className="field__error" role="alert">{submitError}</p>
      )}
      <button type="submit" className="button button--primary" disabled={isDisabled || isSaving}>
        {isSaving ? 'Escalating...' : 'Escalate ticket'}
      </button>
    </form>
  );
}
