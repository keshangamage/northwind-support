import { Link, NavLink, Outlet } from 'react-router-dom';
import { setSession } from './features/auth/session';
import { useSession } from './features/auth/useSession';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const session = useSession();
  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>
        {session && (
          <div className="app__session">
            <span>{session.user.fullName}</span>
            <button className="button" onClick={() => setSession(null)}>Log out</button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
