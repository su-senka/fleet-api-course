# Week 4 - Validation: RFC 9457 in a form

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   API course week 5 (Validation and error responses), resource Drivers
    Fallback   Drivers have no reference implementation anywhere, by design. If your own API
               has not reached week 5, do this week against Vehicles at :5100 instead - its
               POST already answers 400/409 problem documents.
    Scaffold   ApiError in ClientApp/src/api/http.ts already lifts `errors` out of the problem
               document into Record<string, string[]>; no consumer reads it yet.

  Planned subject
    Turning the API's own validation output into a form a person can fix. A controlled form
    (plain React state - no form library this week), submit, and on 400 map ApiError.errors
    onto the fields it names, falling back to a form-level message for the keys that match no
    field. The arguments: client-side validation duplicates the server's rules and will drift
    from them, so what is it actually for; what the client does when the server rejects a field
    the form does not render; why a 409 is not a field error; and keeping the mapping in one
    helper rather than in every form.

  Not this week
    A form library, optimistic updates, file inputs.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

_To be written._

## What to build

_To be written._

## What to hand in

_To be written._
