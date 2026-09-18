import type { ReactNode } from 'react';
import { SignInPage } from '../pages/SignInPage';
import { useUser } from './useUser';

/**
 * Route guard: renders its children only for a signed-in session.
 *
 * This is a usability control, not a security one. It stops a signed-out user seeing an empty
 * page full of failed requests - it does not stop anyone reading data, because the data never
 * arrives without a session cookie the API also checks. If this component were the only thing
 * standing between a user and a record, the application would be broken.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { data: user, isLoading } = useUser();

  // Render nothing rather than the sign-in page while the answer is still unknown: flashing a
  // "please sign in" at an already-authenticated user is the most common bug in this component.
  if (isLoading) {
    return <p className="state-message">Loading…</p>;
  }

  if (!user) {
    return <SignInPage />;
  }

  return <>{children}</>;
}
