import { login } from '../auth/useUser';

/**
 * What `RequireAuth` renders for a visitor who has no session.
 *
 * There is nothing to fetch here and nothing to validate - the only job of this page is to start
 * an OIDC navigation, which is why it exists separately from `RequireAuth` rather than inline in
 * it: the guard's job is deciding whether a session exists, not presenting the way back to one.
 */
export function SignInPage() {
  // login() with no argument captures the current location itself, so the OIDC round trip lands
  // back on the page that required a session rather than on some fixed page.
  return (
    <div className="sign-in">
      <div className="card sign-in__card">
        <h1>Sign in required</h1>
        <p>You need a session to see this page.</p>
        <button type="button" className="button button-primary" onClick={() => login()}>
          Sign in
        </button>
      </div>
    </div>
  );
}
