# Week 9 - Retries, double submits and Idempotency-Key

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   API course week 10 (Idempotency and safe retries)
    Fallback   POST /bookings at :5100 already honours Idempotency-Key (.WithIdempotency()).

  Planned subject
    What the client owes a server that promises safe retries. Generate a key per user intent -
    crypto.randomUUID(), created when the form is opened rather than when the request is sent,
    because a key regenerated on each attempt defeats the entire mechanism - send it on the
    POST, and retry a timed-out or 5xx mutation with the same key. Where the key lives so it
    survives a component remount; why React Query's retry defaults are off for mutations and
    why turning them on is only safe with this header; and demonstrating the double-submit that
    is now harmless.

  Not this week
    Offline queues, background sync, persisting the cache.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

_To be written._

## What to build

_To be written._

## What to hand in

_To be written._
