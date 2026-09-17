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
