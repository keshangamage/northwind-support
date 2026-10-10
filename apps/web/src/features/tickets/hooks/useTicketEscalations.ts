import { useCallback } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';

export function useTicketEscalations(id: number) {
  const load = useCallback(() => ticketsApi.getEscalations(id), [id]);
  return useAsyncData(load, 'Unable to load escalation history.');
}
