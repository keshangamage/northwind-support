import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { authApi } from '../../api/auth';
import { toErrorMessage } from '../../api/client';
import { Field } from '../../components/Field';
import { setSession } from './session';
import { useSession } from './useSession';

export function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState<string>();
  const [isPending, setIsPending] = useState(false);
  const session = useSession();
  const location = useLocation();
  const navigate = useNavigate();
  const from: unknown = (location.state as { from?: unknown } | null)?.from;
  const returnTo = typeof from === 'string' && from.startsWith('/')
    && !from.startsWith('//') && !from.startsWith('/\\') ? from : '/tickets';

  if (session) {
    return <Navigate to={returnTo} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (isPending) return;
    const validation: Record<string, string> = {};
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim()) || email.trim().length > 256) {
      validation.email = 'Enter a valid email address.';
    }
    if (password.length === 0 || password.length > 512) {
      validation.password = 'Enter a password of at most 512 characters.';
    }
    setErrors(validation);
    setError(undefined);
    if (Object.keys(validation).length > 0) return;

    setIsPending(true);
    try {
      const result = await authApi.login(email.trim(), password);
      setSession(result);
      navigate(returnTo, { replace: true });
    } catch (failure) {
      setError(toErrorMessage(failure, 'Could not sign in.'));
    } finally {
      setIsPending(false);
    }
  };

  return (
    <main className="login-page">
      <form className="card form" onSubmit={submit} noValidate>
        <h1>Northwind Support</h1>
        <p>Sign in with your agent account.</p>
        <Field id="login-email" label="Email" error={errors.email}>
          {(props) => <input {...props} type="email" autoComplete="username" value={email}
            disabled={isPending} onChange={(event) => setEmail(event.target.value)} />}
        </Field>
        <Field id="login-password" label="Password" error={errors.password}>
          {(props) => <input {...props} type="password" autoComplete="current-password" value={password}
            disabled={isPending} onChange={(event) => setPassword(event.target.value)} />}
        </Field>
        {error && <p className="field__error" role="alert">{error}</p>}
        <button type="submit" className="button button--primary" disabled={isPending}>
          {isPending ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
    </main>
  );
}
