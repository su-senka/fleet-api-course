# Week 6 - Files: uploading a certificate, downloading something that is not JSON

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   API course week 6 (Sub-resources, and serving something that is not JSON)
    Blocked by that week - Certificates have no reference implementation anywhere, so this
    week genuinely cannot run against :5100. If the API course is behind, swap this week with
    week 7.
    Scaffold   http.ts serialises every body as JSON and parses every response as text/JSON.
               Both assumptions break here, and fixing them without wrecking the wrapper for
               everyone else is the exercise.

  Planned subject
    Two request shapes the typed wrapper was not built for. Upload: multipart/form-data, which
    means not setting Content-Type by hand and letting the browser write the boundary - the
    single most common failure. Download: a blob response with a Content-Disposition filename,
    fetched through the BFF so the session cookie still applies, which is why a plain <a href>
    to the API is not the answer. Also: accept/size validation before the request, and what the
    UI shows while a large file is in flight.

  Not this week
    Resumable or chunked uploads, drag-and-drop polish, a progress bar that needs XHR.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

_To be written._

## What to build

_To be written._

## What to hand in

_To be written._
