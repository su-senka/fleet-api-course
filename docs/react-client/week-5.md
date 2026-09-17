# Week 5 - The generated client: contracts from OpenAPI

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   nothing new - the API publishes /openapi/v1.json from week 1
    Scaffold   ClientApp/src/api/contracts.ts is hand-written and says in its own header that
               generating it is the right answer later; this is later.

  Planned subject
    Replace the hand-written contracts with types generated from the API's OpenAPI document,
    and keep src/api/clients/ as the hand-written layer on top. What this week is really
    teaching: the generated file is build output, not source - it is regenerated, never edited;
    a generation step that nobody runs is worse than no generation at all, so it needs an npm
    script and a check that fails when the checked-in output is stale; and generated enums are
    where the week-1 number-to-label argument gets settled one way or the other.

  Open decision for the course author
    Which generator (openapi-typescript for types only, versus a full client generator). Types
    only keeps src/api/clients/ meaningful and is the smaller diff; note the choice here before
    writing the week, because the reviewer checklist depends on it.

  Not this week
    Generating React Query hooks, runtime response validation (zod).

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

_To be written._

## What to build

_To be written._

## What to hand in

_To be written._
