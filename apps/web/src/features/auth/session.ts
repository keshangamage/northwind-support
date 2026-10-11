import type { AuthSession } from '../../types/api';

const storageKey = 'northwind.session';
const listeners = new Set<() => void>();
let session = readSession();

function readSession(): AuthSession | null {
  try {
    const saved = sessionStorage.getItem(storageKey);
    if (!saved) return null;
    const value = JSON.parse(saved) as AuthSession;
    if (typeof value.token === 'string' && value.token.length > 0
      && Date.parse(value.expiresAtUtc) > Date.now()
      && Number.isInteger(value.user?.id)
      && typeof value.user.fullName === 'string' && typeof value.user.email === 'string') {
      return value;
    }
  } catch {
    // Ignore an invalid saved session.
  }
  sessionStorage.removeItem(storageKey);
  return null;
}

export function getSession(): AuthSession | null {
  return session;
}

export function setSession(value: AuthSession | null): void {
  session = value;
  if (value) sessionStorage.setItem(storageKey, JSON.stringify(value));
  else sessionStorage.removeItem(storageKey);
  listeners.forEach((listener) => listener());
}

export function subscribeSession(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}
