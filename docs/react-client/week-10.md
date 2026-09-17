# Week 10 - Testing the client: Vitest, Testing Library and MSW

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   nothing - this week has no API dependency, which is deliberate: it sits where
               the API course is on weeks 11-12 and the client would otherwise be waiting.
    Scaffold   vitest, jsdom, @testing-library/react and @testing-library/jest-dom are already
               in devDependencies; vite.config.ts already has the test block and
               src/test/setup.ts exists. Nothing imports any of it - there is not one test in
               the repository. MSW is the one dependency this week adds.

  Planned subject
    Test the parts that have actually broken in the preceding weeks: the four render states of
    a list, the validation mapping from a 400 problem document onto fields, and the optimistic
    rollback from week 8. MSW so the tests exercise the real fetch wrapper rather than a
    hand-stubbed module; queries as the user sees them (getByRole, not test ids); a fresh
    QueryClient per test with retry off, or every failure test waits for retries it does not
    want.

  Not this week
    End-to-end tests through a browser, coverage thresholds, snapshot tests.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

_To be written._

## What to build

_To be written._

## What to hand in

_To be written._
