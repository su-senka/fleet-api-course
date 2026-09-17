# Week 8 - Concurrency: ETags, If-Match and optimistic updates

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   API course week 9 (Concurrency: ETags and If-Match), resource Bookings
    Fallback   Bookings at :5100 already set a strong ETag from the row version and require
               If-Match on reschedule and cancel (src/Fleet.Api/Endpoints/BookingEndpoints.cs),
               so this week is never blocked.

  Planned subject
    Two things that belong together: carrying an ETag from a GET into the next write, and
    updating the cache before the server has answered. Read the ETag response header (the
    wrapper currently discards headers entirely - extending it is part of the work), keep it
    next to the cached entity, send If-Match, and handle 412 as a real outcome with a UI that
    tells the user someone else changed this and offers them the current version. Optimistic
    updates via onMutate/onError/onSettled, with the rollback actually tested - an optimistic
    update without a rollback is a lie the UI tells until the next refetch.

  Not this week
    Real-time invalidation, merge/diff UI for conflicting edits.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

_To be written._

## What to build

_To be written._

## What to hand in

_To be written._
