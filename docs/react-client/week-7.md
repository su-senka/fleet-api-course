# Week 7 - Roles in the UI, and what the client may not decide

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   API course weeks 7-8 (Authentication, Authorization)
    Fallback   Bookings are implemented in full at :5100 with the policies already enforced.
    Scaffold   /bff/user already returns { name, roles } (ClientApp/src/auth/types.ts), and
               its own comment says this is presentation data, not an authorization boundary.
               RequireAuth exists and is finished in week 2.

  Planned subject
    Render the application differently for a dispatcher and a driver without believing for a
    moment that this secured anything. Route-level guards by role, conditional controls, and -
    the part that is usually missing - a real answer for the 403 that arrives anyway, because
    the API is the one deciding. The realm ships six users across three roles
    (infra/keycloak/realm-export.json); signing in as each of them is the demonstration.

  Not this week
    Per-resource permission checks in the client, refresh-token handling.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

_To be written._

## What to build

_To be written._

## What to hand in

_To be written._
