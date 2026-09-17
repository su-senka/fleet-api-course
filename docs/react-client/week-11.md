# Week 11 - Long-running work: 202, polling and cancellation

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   API course week 13 (Long-running work: 202 Accepted and polling), Reports
    Blocked by that week - Reports have no reference implementation anywhere, so :5100 is not
    a fallback here. This is why the week sits this late in the arc.

  Planned subject
    A request that answers 202 and a Location, and a UI that has to stay honest for the next
    two minutes. Poll the status resource with refetchInterval as a function of the data,
    stopping the moment the job reaches a terminal state - a poll that never stops is the bug
    this week exists to prevent. Backing the interval off, surviving a page reload (the job id
    belongs in the URL, the same argument as week 1's page number), and telling the user the
    difference between queued, running and failed.

  Not this week
    WebSockets or server-sent events, background notifications.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

_To be written._

## What to build

_To be written._

## What to hand in

_To be written._
