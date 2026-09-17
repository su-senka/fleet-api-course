import { useQuery } from '@tanstack/react-query';
import { ApiError, http, UNAUTHORIZED } from '../api/http';
import type { UserInfo } from './types';

export const userQueryKey = ['bff', 'user'] as const;

/**
 * Fetches the session, treating 401 as an answer rather than a failure.
 *
 * "Nobody is signed in" is a perfectly good outcome of asking who is signed in. Letting the 401
 * throw would put the query into an error state and make every consumer distinguish "anonymous"
 * from "the network is down" by reading the error - so do it once, here.
 */
async function fetchUser(): Promise<UserInfo | null> {
  try {
    return await http.get<UserInfo>('/bff/user');
  } catch (error) {
    if (error instanceof ApiError && error.status === UNAUTHORIZED) {
      return null;
    }
    throw error;
  }
}

/** The current session, or null when anonymous. */
export function useUser() {
  return useQuery({
    queryKey: userQueryKey,
    queryFn: fetchUser,
    staleTime: 60_000,
    // Retrying an anonymous session three times just delays the sign-in page.
    retry: false,
  });
}

/**
 * Sign-in is a full-page navigation, not a fetch.
 *
 * OIDC needs the browser to visit Keycloak, which needs a real navigation - an XHR cannot show
 * a login form, and following the redirect in JavaScript would defeat the point of the flow.
 * `returnUrl` is where the BFF sends the user back to afterwards.
 */
export function login(returnUrl?: string) {
  const target = returnUrl ?? window.location.pathname + window.location.search;
  window.location.assign(`/bff/login?returnUrl=${encodeURIComponent(target)}`);
}

/** Also a navigation: the BFF clears the cookie and signs the user out of Keycloak too. */
export function logout() {
  window.location.assign('/bff/logout');
}
