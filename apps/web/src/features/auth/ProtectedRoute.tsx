import { useEffect } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { setSession } from './session';
import { useSession } from './useSession';

export function ProtectedRoute() {
  const session = useSession();
  const location = useLocation();
  const expiresAt = session ? Date.parse(session.expiresAtUtc) : 0;

  useEffect(() => {
    if (!session) return;
    const timer = window.setTimeout(() => setSession(null), Math.max(0, expiresAt - Date.now()));
    return () => window.clearTimeout(timer);
  }, [session, expiresAt]);

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search + location.hash }} />;
  }

  return <Outlet />;
}
