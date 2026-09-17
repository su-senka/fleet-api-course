# Week 12 - Shipping it: one origin, code splitting and caching

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   API course weeks 11 and 14 (Caching; Documentation and versioning) loosely
    Scaffold   vite.config.ts builds to ClientApp/dist and its header already states the
               production arrangement - the BFF serves the bundle, so the SPA and /api are the
               same origin and every relative path from week 1 finally pays off. The host does
               not serve static files yet.

  Planned subject
    Make the two halves one deployable. Serve the built bundle from the BFF with a SPA fallback
    that does not swallow /api or /bff; get the caching right (hashed assets immutable,
    index.html never cached - the reverse of the intuition, and the reason a deployed SPA
    serves yesterday's bundle); build the ClientApp from the .csproj so a publish produces
    both; split the routes with lazy + Suspense and look at what actually ships. Then the look
    back: what the BFF bought, what it cost, and which of these twelve decisions would be
    different in a smaller application.

  Not this week
    A CDN, SSR, Docker or CI pipeline work beyond the publish step.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

_To be written._

## What to build

_To be written._

## What to hand in

_To be written._
