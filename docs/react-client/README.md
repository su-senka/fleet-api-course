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

| Week | Title | What it is really about |
|---|---|---|
| 1 | The skeleton, and the first real call | Project structure, the typed fetch wrapper, React Query, three render states |
| 2 | The BFF: sessions without tokens in the browser | OIDC code flow, the HttpOnly cookie, CSRF, the token-attaching proxy |

Weeks 1 and 2 are written. The rest of the arc is the course author's to write, and the obvious
candidates are generating the client from OpenAPI, forms and mutations against the API's RFC 9457
validation errors, optimistic updates with `If-Match`, and what to do about the `Idempotency-Key`
the API starts honouring in week 10.

## Why a BFF

Because the alternative is a token in the browser. Any JavaScript on the page can read
`localStorage`, and one compromised dependency is enough - a token stolen there is valid at the API
until it expires, from anywhere. An `HttpOnly` cookie cannot be read by script at all; what it buys
in exchange is CSRF, which is a solved problem with a known defence.

So the browser holds a cookie, the BFF holds the token, and the join between them is one request
transform. That is the pattern the .NET and React communities converged on for enterprise
applications, and it is what `src/Fleet.Web.Workshop` is shaped like.

The reference this scaffold was modelled on is `TaskTracker/src/TT.Web`, if you have it to hand.
