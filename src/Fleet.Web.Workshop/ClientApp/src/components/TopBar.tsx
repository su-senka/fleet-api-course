import { NavLink } from 'react-router';
import { login, logout, useUser } from '../auth/useUser';

const links = [
  { to: '/', label: 'Home', end: true },
  { to: '/depots', label: 'Depots', end: false },
  { to: '/vehicles', label: 'Vehicles', end: false },
];

/**
 * Brand, navigation and the session, in one bar above every page.
 *
 * Always rendered, signed in or not: an anonymous visitor gets the same nav plus a "Sign in"
 * button, rather than a bar that silently does nothing until a page underneath asks for a
 * session. It is presentation only - hiding a name or a link here changes what is on screen, not
 * what the API will accept.
 */
export function TopBar() {
  const { data: user } = useUser();

  return (
    <header className="top-bar">
      <div className="container top-bar__inner">
        <span className="top-bar__brand">Fleet</span>

        <nav className="top-bar__nav">
          {links.map(({ to, label, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                `top-bar__link${isActive ? ' top-bar__link--active' : ''}`
              }
            >
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="top-bar__account">
          {user ? (
            <>
              <span className="top-bar__user">{user.name}</span>
              <button type="button" className="button" onClick={logout}>
                Sign out
              </button>
            </>
          ) : (
            <button type="button" className="button button-primary" onClick={() => login()}>
              Sign in
            </button>
          )}
        </div>
      </div>
    </header>
  );
}
