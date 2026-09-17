# Week 3 - Mutations: creating and changing from the browser

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   API course week 4 (Creating and changing resources), resource Vehicles
    Fallback   Vehicles are implemented in full at the reference API on :5100, so this week is
               not blocked if your own POST /vehicles is not finished yet.
    Scaffold   ClientApp/src/api/http.ts already has post/put/delete and already sets
               Content-Type when a body is present; nothing in src/ calls them yet.

  Planned subject
    The write half of React Query: useMutation, and what happens to the cache afterwards.
    Register a vehicle (POST /api/vehicles) and change its status (PUT
    /api/vehicles/{id}/status) from the vehicles page. The decisions worth an argument:
    invalidateQueries versus setQueryData; which keys a write invalidates and why a broad key
    is usually right first; disabling the submit control on isPending rather than trusting the
    user not to double-click (which is week 9's subject properly); where a created resource's
    id comes from and whether to navigate to it; and keeping the mutation functions in
    src/api/clients/ so components still never touch fetch.

  Not this week
    Field-level validation display (week 4), optimistic updates (week 8), Idempotency-Key
    (week 9), a form library.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

_To be written._

## What to build

_To be written._

## What to hand in

_To be written._
