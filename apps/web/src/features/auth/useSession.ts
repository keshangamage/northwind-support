import { useSyncExternalStore } from 'react';
import { getSession, subscribeSession } from './session';

export function useSession() {
  return useSyncExternalStore(subscribeSession, getSession);
}
