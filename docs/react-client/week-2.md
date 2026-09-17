# Week 2 - The BFF: sessions without tokens in the browser

<!--
  What is already true, and does not need restating here:
    Scaffold    src/Fleet.Web.Workshop/ (host) and ClientApp/ (SPA)
    Realm       infra/keycloak/realm-export.json - six users, three roles
    Reference   TaskTracker/src/TT.Web, the same pattern against a different IdP

  Every piece the students write is stubbed with a TODO(week-2) marker where it belongs.
-->

## Goal

Sign a user in, and do it without the browser ever holding a token.

The obvious design - fetch a token from Keycloak in JavaScript, keep it in `localStorage`, attach
it to every request - is the one to argue against first, because it is what most tutorials show.
Any script running on the page can read `localStorage`; one compromised npm dependency, one
injected script, and the token is exfiltrated and valid at the API from anywhere until it expires.
There is no way to revoke it from the browser and no way to notice.

The alternative is a **BFF**: a small server-side host that owns the session. The browser gets an
`HttpOnly` cookie, which script cannot read at all. The token stays on the server and is attached
to API calls as they pass through. What this costs you is CSRF - a cookie is sent by the browser
on every request to that origin, including ones another site triggered - and that is a bounded,
solved problem, which the token-in-`localStorage` design's problem is not.

`src/Fleet.Web.Workshop/` is that host, scaffolded and running but doing none of this yet. Four
things happen this week, and each has a `TODO(week-2)` where it goes:

1. A cookie session in front, an OIDC authorization-code flow behind it.
2. Three routes - `/bff/login`, `/bff/logout`, `/bff/user` - that are the whole contract between
   the SPA and the session.
3. A CSRF guard on the proxied API.
4. A reverse proxy that turns the cookie back into a bearer token on the way to the Fleet API.

This week also changes what the SPA talks to. In week 1 Vite proxied `/api` straight to your own
API on `:5101`, which had no authentication. Now `/api` goes to the BFF on `:5180`, and the BFF
forwards to the **reference** API on `:5100` - which does require a token, and has since week 7 of
the API course. That is the point: the two courses meet here.

## What to build

In `src/Fleet.Web.Workshop/`:

- **The session** (`Extensions/ServiceCollectionExtensions.cs`). Cookie as the default scheme, OIDC
  as the challenge and sign-out scheme. Getting the default scheme wrong is the classic failure -
  every request re-challenges, the user bounces to Keycloak on each click, and the session appears
  never to stick. The cookie must be `HttpOnly`; `SameSite=Lax` is what lets the OIDC callback
  work as a top-level GET redirect.
- **Two cookie events that make a SPA possible at all**: `OnRedirectToLogin` must answer `401` and
  `OnRedirectToAccessDenied` must answer `403`, instead of redirecting. An XHR cannot usefully
  follow a redirect to an HTML login page - it either fails CORS or hands your code Keycloak's
  markup where JSON was expected. The SPA needs a status code to branch on, which is exactly what
  `src/auth/useUser.ts` already does with the 401.
- **The OIDC handler**, configured from `Authentication:Oidc` (already in `appsettings.json`,
  pointing at the `fleet` realm). Authorization code flow, `SaveTokens = true` so the proxy can
  reach the access token later. You will need a `fleet-web` client in Keycloak - the realm ships
  `fleet-api`, which is the API's audience, not a browser client. Adding it is part of the work;
  say in your PR whether you made it public or confidential and why.
- **The three BFF routes** (`Extensions/EndpointsBuilderExtensions.cs`). `/bff/login` issues the
  challenge and returns to `returnUrl` - **validate that parameter**, or you have built an open
  redirect that launders an attacker's link through your trusted domain. Accept only paths
  starting with a single `/`, and note that `//evil.example` also starts with `/`. `/bff/user`
  returns the display name and roles for an authenticated caller and `401` otherwise, and must
  allow anonymous callers: "am I signed in?" has to be answerable by someone who is not.
- **The CSRF guard** (`Middleware/CsrfHeaderMiddleware.cs`): reject any `/api` request without the
  `X-CSRF` header. A cross-site form post or a top-level navigation cannot set a custom header,
  so its presence proves the request came from our own JavaScript. The value carries no
  information and is not a secret. Then uncomment the matching header in `ClientApp/src/api/http.ts`.
- **The proxy** (`AddApiProxy`): YARP, configured from `appsettings.json`, with a request transform
  that reads the access token out of the session and sets `Authorization: Bearer`. Point the
  cluster at the reference API on `:5100`.

In `ClientApp`:

- **Re-point Vite** at the BFF and forward the session routes (`vite.config.ts` has the four lines
  commented out, with a note about which failure you get if you forget them).
- **Finish `RequireAuth`** so an anonymous user gets a sign-in page that calls `login()`, and wrap
  the routes that need a session in `App.tsx`.
- **Show who is signed in**, and a sign-out control, using `useUser()`.

**Do not add:** role-based hiding of UI beyond what is needed to demonstrate the session, refresh
token rotation, forms or mutations, or `Idempotency-Key` handling. And do not treat `/bff/user` as
an authorization boundary - it decides which menu items render, while the API decides what actually
happens. A client that hides a button has secured nothing.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The session, the three routes, the CSRF guard, the authenticated proxy, and the SPA changes.
- A short PR description explaining, in your own words, what an attacker gains from a token in
  `localStorage` that they do not gain from your `HttpOnly` cookie - and what you had to add
  because of the cookie. State your `fleet-web` client choice and your `returnUrl` validation.
- Evidence it works: paste or screenshot at least five things - `/bff/user` returning `401` while
  signed out; the Keycloak sign-in round trip landing back on the page you started from;
  `/bff/user` returning your name and roles; an authenticated `/api/bookings` call succeeding
  through the proxy; and a `/api` call with the `X-CSRF` header removed being rejected. Then open
  devtools and show that the session cookie is `HttpOnly` and that no token appears anywhere in
  `localStorage`, `sessionStorage` or the JavaScript heap.

**Reviewer checklist:**
- [ ] The host builds and the client's `npm run build` and `npm run lint` pass
- [ ] No access token is reachable from JavaScript; the session cookie is `HttpOnly`
- [ ] An anonymous XHR to `/api` gets `401`, not a redirect to Keycloak
- [ ] `/bff/login?returnUrl=https://evil.example` does not leave your origin
- [ ] An `/api` request without `X-CSRF` is rejected
- [ ] The proxy attaches the token; `/api/bookings` returns the caller's bookings from `:5100`
- [ ] Signing out clears the session at Keycloak too, not only locally
- [ ] PR description argues the cookie-versus-`localStorage` trade honestly, including its cost
