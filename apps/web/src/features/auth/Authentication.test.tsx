import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { request } from '../../api/client';
import type { AuthSession } from '../../types/api';
import { LoginPage } from './LoginPage';
import { ProtectedRoute } from './ProtectedRoute';
import { getSession, setSession } from './session';

const fetchMock = vi.fn<typeof fetch>();

function session(): AuthSession {
  return {
    token: 'test-token', expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
  };
}

function TicketPage() {
  return <button onClick={() => { void request('/tickets').catch(() => {}); }}>Load tickets</button>;
}

function renderRoutes(path = '/tickets') {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<ProtectedRoute />}>
          <Route element={<App />}>
            <Route path="/tickets" element={<TicketPage />} />
            <Route path="/tickets/:id" element={<p>Ticket details</p>} />
          </Route>
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

function signIn(email = 'alex.turner@northwind-support.example', password = 'Test@1234') {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('Authentication', () => {
  beforeEach(() => {
    setSession(null);
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => {
    cleanup();
    setSession(null);
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('redirects a visitor to login and returns to the requested page after sign-in', async () => {
    const result = session();
    fetchMock.mockResolvedValue(new Response(JSON.stringify(result)));
    renderRoutes('/tickets/12?tab=history');
    expect(screen.getByLabelText('Email')).toBeVisible();
    signIn('  alex.turner@northwind-support.example  ');
    expect(await screen.findByText('Ticket details')).toBeVisible();
    expect(screen.getByText('Alex Turner')).toBeVisible();
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/login', expect.objectContaining({
      method: 'POST', body: JSON.stringify({
        email: 'alex.turner@northwind-support.example', password: 'Test@1234',
      }),
    }));
    expect(JSON.parse(sessionStorage.getItem('northwind.session')!)).toEqual(result);
  });

  it('blocks invalid fields before sending a request', () => {
    renderRoutes('/login');
    signIn('invalid-email', '');
    expect(screen.getByText('Enter a valid email address.')).toBeVisible();
    expect(screen.getByText('Enter a password of at most 512 characters.')).toBeVisible();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('shows the generic error for rejected credentials', async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ detail: 'Email or password is incorrect.' }), { status: 401 }));
    renderRoutes('/login');
    signIn();
    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.');
    expect(getSession()).toBeNull();
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
  });

  it('disables submission while login is pending', async () => {
    let finish!: (response: Response) => void;
    fetchMock.mockImplementation(() => new Promise((resolve) => { finish = resolve; }));
    renderRoutes('/login');
    signIn();
    expect(screen.getByRole('button', { name: 'Signing in...' })).toBeDisabled();
    expect(screen.getByLabelText('Password')).toBeDisabled();
    fireEvent.submit(screen.getByRole('button', { name: 'Signing in...' }).closest('form')!);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    finish(new Response(JSON.stringify(session())));
    expect(await screen.findByRole('button', { name: 'Load tickets' })).toBeVisible();
  });

  it('attaches the token and redirects to login on a 401', async () => {
    setSession(session());
    fetchMock.mockResolvedValue(new Response('', { status: 401 }));
    renderRoutes();
    fireEvent.click(screen.getByRole('button', { name: 'Load tickets' }));
    expect(await screen.findByLabelText('Email')).toBeVisible();
    const headers = new Headers(fetchMock.mock.calls[0][1]?.headers);
    expect(headers.get('Authorization')).toBe('Bearer test-token');
    expect(getSession()).toBeNull();
    expect(sessionStorage.getItem('northwind.session')).toBeNull();
  });

  it('shows the agent name and clears the session on logout', async () => {
    setSession(session());
    renderRoutes();
    expect(screen.getByText('Alex Turner')).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Log out' }));
    expect(await screen.findByLabelText('Email')).toBeVisible();
    expect(getSession()).toBeNull();
    expect(sessionStorage.getItem('northwind.session')).toBeNull();
  });

  it('clears an expired session even when no request is sent', async () => {
    vi.useFakeTimers();
    const result = session();
    result.expiresAtUtc = new Date(Date.now() + 1000).toISOString();
    setSession(result);
    renderRoutes();
    await act(async () => { vi.advanceTimersByTime(1000); });
    expect(screen.getByLabelText('Email')).toBeVisible();
    expect(getSession()).toBeNull();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('keeps a valid session when an API request fails with 409', async () => {
    const result = session();
    setSession(result);
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ detail: 'Conflict' }), { status: 409 }));
    await expect(request('/tickets/1/escalate')).rejects.toMatchObject({ status: 409 });
    expect(getSession()).toEqual(result);
  });

  it('rejects an expired token before sending an API request', async () => {
    const result = session();
    result.expiresAtUtc = new Date(Date.now() - 1000).toISOString();
    setSession(result);
    await expect(request('/tickets')).rejects.toMatchObject({ status: 401 });
    expect(fetchMock).not.toHaveBeenCalled();
    expect(getSession()).toBeNull();
  });

  it('preserves custom request headers when adding the token', async () => {
    setSession(session());
    fetchMock.mockResolvedValue(new Response('{}'));
    await request('/auth/me', { headers: new Headers({ 'X-Request-Id': 'test-request' }) });
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const headers = new Headers(fetchMock.mock.calls[0][1]?.headers);
    expect(headers.get('X-Request-Id')).toBe('test-request');
    expect(headers.get('Authorization')).toBe('Bearer test-token');
  });
});
