# React client course

A second course, over the same Fleet domain and the same Keycloak realm as the API course. Where
that one builds the HTTP layer over finished modules, this one builds the browser client over a
finished HTTP layer - and then the host that stands between them.

The scaffold is `src/Fleet.Web.Workshop/`. Two halves in one project:

| Half | What it is | Runs on |
|---|---|---|
| `src/Fleet.Web.Workshop/` | The BFF: a .NET host that owns the session and proxies `/api` | `:5180` |
| `src/Fleet.Web.Workshop/ClientApp/` | The React application - Vite, TypeScript, React Query | `:5174` (dev) |

It is deliberately **not** in `Fleet.sln`: a solution build should not depend on npm. Build and run
it by pointing `dotnet` at the project file directly.

## The dev loop

Three terminals, and the order matters only for the first one:

```
make up                                              # the Compose stack, if it is not running
dotnet run --project src/Fleet.Api.Workshop          # your API from the other course, :5101
cd src/Fleet.Web.Workshop/ClientApp && npm install && npm run dev   # the SPA, :5174
```

**Node 22.22 or newer** is required, and `package.json` says so in `engines`. That floor is not
ours: React Router 8 sets it, and Vite 8 wants a matching `@types/node`. On an older Node the
install appears to succeed and the dev server fails later, which is a bad half-hour.

Open <http://localhost:5174>. In week 1 that is all you need - Vite proxies `/api` straight to the
API on `:5101`, and the BFF host is not in the picture yet.

From week 2 you also run the BFF, and Vite proxies to it instead:

```
dotnet run --project src/Fleet.Web.Workshop          # :5180
```

## The arc

Twelve weeks. Weeks 1 and 2 are written; weeks 3-12 exist as stubs whose HTML comment header
records the planned subject, what the week leans on, and what is deliberately left for a later
week. The specification itself is the course author's to write, one week at a time.

| Week | Title | What it is really about | Leans on |
|---|---|---|---|
| 1 | The skeleton, and the first real call | Project structure, the typed fetch wrapper, React Query, three render states | - |
| 2 | The BFF: sessions without tokens in the browser | OIDC code flow, the HttpOnly cookie, CSRF, the token-attaching proxy | API 7 |
| 3 | Mutations: creating and changing from the browser | `useMutation`, cache invalidation versus `setQueryData`, pending state | API 4 |
| 4 | Validation: RFC 9457 in a form | Mapping the API's `errors` extension onto fields; who validates what | API 5 |
| 5 | The generated client: contracts from OpenAPI | Generated types as build output, staleness checks, enums settled | - |
| 6 | Files: uploading a certificate, downloading something that is not JSON | `multipart/form-data`, blob responses, why a plain link to the API fails | API 6 |
| 7 | Roles in the UI, and what the client may not decide | Role-gated routes and controls, and handling the 403 that still arrives | API 7-8 |
| 8 | Concurrency: ETags, If-Match and optimistic updates | Carrying an ETag from GET to PUT, optimistic writes, real rollback, 412 | API 9 |
| 9 | Retries, double submits and Idempotency-Key | One key per intent, surviving remount, making mutation retries safe | API 10 |
| 10 | Testing the client: Vitest, Testing Library and MSW | The four states, the validation mapping, the rollback - against real `fetch` | - |
| 11 | Long-running work: 202, polling and cancellation | Polling that stops, backs off, and survives a reload | API 13 |
| 12 | Shipping it: one origin, code splitting and caching | Serving the bundle from the BFF, cache headers, lazy routes, a look back | API 11, 14 |

## What this arc deliberately leaves out

Worth saying plainly, so nobody finishes the course thinking these do not exist.

**React 19 form actions** - `<form action={fn}>`, `useActionState`, `useFormStatus`,
`useOptimistic`. These are baseline React now and a 2026 codebase will contain them. We build
weeks 3, 4 and 8 on `useMutation` instead, because form actions solve the half of the problem this
course is about the *other* half of: they give you a pending state and a return value for one
submission, and they do not model the fact that a successful `POST /vehicles` makes a cached
`GET /vehicles?page=1` untrue. The current consensus is a split rather than a winner - the action
owns the UI handshake, the data layer owns cache correctness - and this app has no server
components to make the action half compelling. Week 3 states this where a student will meet it.

**The React Compiler.** Stable since 1.0 and on by default in new Vite scaffolds. We take its lint
rules (see the toolchain section) and leave the compiler off, so memoisation stays something you
decide rather than something a build step does behind you.

**TanStack Router.** React Router 8 in declarative mode is what the scaffold uses. TanStack Router
has better type-safe routing and pairs naturally with TanStack Query; it is a defensible choice and
would change weeks 1 and 2 substantially.

If the arc ever grows a thirteenth week, the first three paragraphs above are what belongs in it.
Week 12's look back is the other natural home.

## Running this alongside the API course

"Leans on" is the API-course week whose work this week consumes. The two arcs are not lock-step:
the API course is fourteen weeks and delivers its first `POST` in week 4, so a React week is
usually one or two weeks behind the API week it needs. Sequence the client course to trail, not
to match numbers.

Where a week is early, there is a way out. `src/Fleet.Api/` implements **Vehicles and Bookings**
in full at `:5100`, with paging, validation problems, `ETag`/`If-Match` and `Idempotency-Key`
already in place - so weeks 3, 4, 7, 8 and 9 can run against the reference API when a student's
own is behind. Point the BFF's YARP cluster at `:5100` instead of `:5101`; that is a one-line
change in `appsettings.json`, and it is what week 2 already does.

Two weeks have no such escape. **Certificates** (week 6) and **Reports** (week 11) have no
reference implementation anywhere, by design - see `docs/assignments/README.md`. Those two weeks
genuinely require the student's own API to have reached API weeks 6 and 13. That constraint is
why week 11 sits where it does, and weeks 6 and 7 can be swapped if the API course is running
behind.

## Why a BFF

Because the alternative is a token in the browser. Any JavaScript on the page can read
`localStorage`, and one compromised dependency is enough - a token stolen there is valid at the API
until it expires, from anywhere. An `HttpOnly` cookie cannot be read by script at all; what it buys
in exchange is CSRF, which is a solved problem with a known defence.

So the browser holds a cookie, the BFF holds the token, and the join between them is one request
transform. That is the pattern the .NET and React communities converged on for enterprise
applications, and it is what `src/Fleet.Web.Workshop` is shaped like.

The reference this scaffold was modelled on is `TaskTracker/src/TT.Web`, if you have it to hand.

This is also, as of this writing, the pattern the OAuth working group itself recommends: the IETF
draft *OAuth 2.0 for Browser-Based Applications* calls the token-mediating backend architecture
"strongly recommended for business applications, sensitive applications, and applications that
handle personal data". We are not being clever here; we are doing the ordinary thing.

## The toolchain, and why these versions

Worth reading once, because two of these pins are deliberate rather than merely current.

| | Version | Note |
|---|---|---|
| .NET | `net10.0`, SDK `10.0.400` | .NET 10 is the current LTS |
| YARP | `2.3.0` | current |
| React | `19.3` | |
| React Router | `8.4` | declarative mode; needs Node 22.22+ |
| TanStack Query | `5.104` | v5 is still the current major |
| Vite | `8.3` | Rolldown is the bundler from 8.0 on |
| Vitest | `5.0` | |
| ESLint | `10.11` | flat config |
| `eslint-plugin-react-hooks` | `7.1` | see below |
| TypeScript | `~5.9.3` | **pinned deliberately** - see below |

**TypeScript is pinned to 5.9 on purpose.** The current release is TypeScript 7, the Go-native
rewrite of the compiler, and it is genuinely much faster. We are not on it, because
`typescript-eslint` declares a peer range of `typescript <6.1.0` and has no release that supports
7: TypeScript 7.0 defers the programmatic Compiler API to 7.1, and type-aware linting is built on
that API. Bumping TypeScript breaks `npm run lint`, which every week's reviewer checklist depends
on, so the lint wins and the compiler waits.

That trade is worth noticing rather than hiding. "Latest" and "adoptable" are different
properties, and the thing that decides which you get is usually not the library you wanted to
upgrade but something downstream of it.

**`eslint-plugin-react-hooks` v7 is doing much more than it used to.** Its `recommended` preset
carried two rules in v5 (`rules-of-hooks`, `exhaustive-deps`) and carries sixteen in v7. The new
ones come out of the React Compiler's analysis, and they reject at lint time several things that
used to be runtime bugs you had to reproduce - `set-state-in-effect`, `set-state-in-render`,
`purity`, `immutability`. If the linter starts objecting to an effect you have written before and
got away with, read the message; it is usually right.

We take those rules **without** enabling the React Compiler itself. Memoisation stays manual, so
`useMemo` and `useCallback` remain decisions you make and can measure, rather than something a
build step quietly does for you. That is a teaching choice, not a recommendation against the
compiler - a production codebase in 2026 would probably turn it on.

## One wire-format fact, and why both hosts agree about it

Enums travel as **names**, not numbers - `"type": "Van"`, `"status": "InMaintenance"` - because
both hosts register `JsonStringEnumConverter`:

| Talking to | `vehicle.type` on the wire |
|---|---|
| your own API, `:5101` (`Fleet.Api.Workshop`) | `"Van"` |
| the reference API, `:5100` (`Fleet.Api`) | `"Van"` |

That agreement is deliberate and it is load-bearing for this course. The client changes which host
it talks to partway through the arc - week 1 proxies to `:5101`, week 2 onwards proxies to `:5100`
through the BFF, and week 4 may point back at `:5101` again. If the two hosts disagreed about a
field's shape, `src/api/contracts.ts` would be correct in week 1 and silently wrong from week 2,
and the failure would hide rather than announce itself: a page that prints a raw value renders a
string perfectly well, so only a status badge's colour would go missing.

So the hosts match on purpose. Two hosts serving one resource in two shapes is not a lesson, it is
a trap, and the course has better things to teach.

What it is still worth noticing is the *kind* of guarantee you have here. `src/api/contracts.ts` is
hand-written, so nothing tells it when the server adds a fourth `VehicleStatus`. Nothing is wrong
today and nothing will warn you on the day it becomes wrong. That is the normal condition of a
hand-written contract, and it is why week 5 replaces it with types generated from the OpenAPI
document.

