# Week 3 - Mutations: creating and changing from the browser

<!--
  What is already true, and does not need restating here:
    Scaffold   src/Fleet.Web.Workshop/ (BFF host) and ClientApp/ (Vite + TypeScript + React Query)
    Leans on   API course week 4 (Creating and changing resources), resource Vehicles
    Fallback   Vehicles are implemented in full at the reference API on :5100, which is where the
               BFF already points, so this week is not blocked if your own POST /vehicles is not
               finished yet.
    Scaffold   ClientApp/src/api/http.ts already has post/put/delete, already sets Content-Type
               when a body is present, and already sends X-CSRF. Nothing in src/ calls them yet.
    Scaffold   index.css gained .form/.field/.input/.field__error/.form-error ahead of this week,
               so neither this week nor week 4 is about CSS.

  Not this week
    Field-level validation display (week 4), optimistic updates (week 8), Idempotency-Key
    (week 9), a form library.

  The scaffold builds, lints and runs before anyone touches it. The assignment is what is
  stubbed, and the decisions it forces.
-->

## Goal

Write to the API from the browser, and leave the cache in a state you can defend.

Reading was the easy half. A `GET` that is wrong shows stale data until the next refetch; a `POST`
that is wrong creates a vehicle twice, or creates one and then shows a list that does not contain
it. This week is the first time the client changes something, and the interesting part is not the
request - `http.post` is already written - it is what happens to everything you cached afterwards.

React Query models that explicitly, and the model is worth learning properly rather than
copying: a query reads and caches, a mutation writes and then **invalidates** what the write made
untrue. Most of the arguing this week is about that second half.

**Sign in as `admin.novak` (password `fleet`).** Both endpoints this week sit behind the
`ManageFleet` policy, which requires the `fleet.admin` realm role. As `dispatch.svoboda` or
`driver.dvorak` every write returns `403` no matter how correct your code is. Do not fix that by
hiding the form - week 7 is about roles in the UI, and the point it makes is that hiding a control
secures nothing. For now, sign in as the admin and let the 403 be week 7's problem.

### Why not React 19's form actions

A fair question in 2026, since `<form action={fn}>`, `useActionState` and `useFormStatus` are
baseline React now. The short answer is that they solve the half of the problem we do not have.
Form actions give you a pending state and a return value for one submission; they do not know that
a successful `POST /vehicles` makes a cached `GET /vehicles?page=1` untrue. That graph of what
invalidates what is the thing React Query exists to model, and the thing this week is about.

The current consensus is a split rather than a winner: let the action own the UI handshake, let the
data layer own cache correctness. We use `useMutation` for both this week because it does both
adequately and because learning one model well beats learning two badly. It is also what a .NET
shop's React client almost always actually contains.

## What to build

In `src/Fleet.Web.Workshop/ClientApp`:

- **A `RegisterVehicle` body type in `src/api/contracts.ts`**, beside the existing `Vehicle`.
  Reuse the `VehicleType` union that is already there rather than typing `type: string` - the
  whole value of a union is that `"Vna"` stops compiling instead of becoming a `400` you have to
  debug through devtools.

- **Two mutation functions in `src/api/clients/vehicles.ts`**, beside `list` and `getById`:
  - `register(body)` → `POST /api/vehicles`, body `{ plate, type, depotId, odometerKm }`. Send
    `type` as the enum **name** (`"Van"`), not a number. The API would accept either - its
    converter reads both forms - but it answers with names, and a client that sends one shape and
    receives another is one refactor away from a bug.
  - `changeStatus(vehicleId, status)` → `PUT /api/vehicles/{vehicleId}/status`, body `{ status }`.

  Components still must not touch `fetch`, or `http`, directly. The rule from week 1 does not
  relax because the verb changed.

- **A registration form.** Plate, type, depot and odometer. Depot is a `<select>` fed by
  `depotsApi.list()` - a free-text GUID field is not a user interface. Plain React state is fine
  and is what this week wants; no form library.

  Put it where you can defend: a `/vehicles/new` route, or a panel on the vehicles page. Say which
  you chose and why in the PR. If you add a route, register it in `App.tsx` inside `RequireAuth`
  and decide whether it belongs in `TopBar`.

- **A status control on each row** of the vehicles table, calling `changeStatus`.

- **Invalidation that is actually correct.** After a successful write, invalidate
  `queryKey: ['vehicles']`. Note what that does: it is a *prefix* match, so it invalidates every
  cached page and every cached filter at once, not just the one you are looking at. That is almost
  always what you want first, and the reason is that you cannot know which page a new vehicle lands
  on - that depends on the sort, the filter and what other people have created since.

- **A disabled submit button while the write is in flight**, from the mutation's `isPending`. This
  is a courtesy, not a safety mechanism: it narrows the window for a double-click, it does not
  close it, and it does nothing at all about a retry or a reload. Making a retry genuinely safe
  needs `Idempotency-Key`, which is week 9. Say in your PR that you know the difference.

- **Handle the failures the API actually returns**, which are not only validation:
  - `409` when the plate is already in the fleet.
  - `404` when the `depotId` does not exist.
  - `409` when you try to move a `Retired` vehicle back to `Available` - retired is terminal.

  One form-level message carrying `ApiError.message` is enough this week. Per-field display is
  week 4, and deliberately so: do that work twice and you will do it badly once.

**Do not add:** optimistic updates (week 8), `Idempotency-Key` (week 9), per-field error display
(week 4), a form library, or a toast library. And do not reach for `setQueryData` to splice the
response into the list - see below, where that is a question rather than a prohibition.

### The argument worth having: `invalidateQueries` or `setQueryData`

`setQueryData` writes the server's response straight into the cache and skips a refetch. It is
tempting here, because `POST /vehicles` returns the created vehicle, so it looks like you already
have the row.

You do not. Read the two shapes:

- the list returns `VehicleDto` - `{ id, plate, type, status, odometerKm, depotId, depotName }`
- the write returns `VehicleDetailDto` - `{ id, plate, type, status, odometerKm, depot: { id, name, city }, lastOdometerReadingAt }`

They are different types on purpose. To splice the response into a cached page you would have to
flatten `depot` into `depotId` and `depotName` by hand, in the client, and that hand-written
conversion is a second implementation of a mapping the server already owns. It will drift.

There is a worse problem than drift. A cached page is a *page* - it has a `pageSize`, a
`totalCount`, a `hasNextPage` the server computed. Inserting a row into it makes every one of
those fields a lie, and you do not know whether your new vehicle sorts onto this page at all.

So: `invalidateQueries` this week, and be able to say when `setQueryData` would be right - it is,
for a single-resource key like `['vehicle', id]` after a `PUT` that returns the whole resource.
Week 8 comes back to this when the write happens before the server has agreed to it.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The corrected `Vehicle` contract, the two mutation functions, the registration form, the status
  control, and invalidation after both writes.
- A short PR description answering three questions. First: `invalidateQueries` or `setQueryData`
  here, and what specifically made you choose - name the two DTO shapes. Second: which key did you
  invalidate, and what would have broken had you invalidated only the page you were looking at.
  Third: where did you get the new vehicle's `id`, and did you navigate to it - why or why not?
- Evidence it works: show at least five things - a vehicle registered and appearing in the list
  without a manual reload; the submit button disabled mid-flight; a duplicate plate rejected with
  the API's own `409` message on screen; a status change reflected in the row; and an attempt to
  revive a `Retired` vehicle being refused.

**Reviewer checklist:**
- [ ] `npm run build` and `npm run lint` both pass
- [ ] The request body type reuses the `VehicleType` union rather than `string`
- [ ] No `fetch` or `http.*` call outside `src/api/`
- [ ] `type` is sent as an enum name, not a number
- [ ] The depot field is a select populated from the API, not a typed GUID
- [ ] A successful write invalidates `['vehicles']`, and the list updates with no manual reload
- [ ] The submit control is disabled while `isPending`
- [ ] A `409` is shown using the API's own message, not "something went wrong"
- [ ] No `Idempotency-Key`, no optimistic update, no per-field error mapping yet
- [ ] The PR names the DTO shape difference as the reason `setQueryData` was not used
