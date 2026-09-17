# Week 1 - The skeleton, and the first real call

<!--
  What is already true, and does not need restating here:
    Scaffold    src/Fleet.Web.Workshop/ClientApp/
    API         src/Fleet.Api.Workshop on :5101 (yours, from the API course)
    Worked example  ClientApp/src/pages/DepotsPage.tsx

  The scaffold builds, lints and runs before anyone touches it. The assignment is the page that
  is stubbed, and the decisions it forces.
-->

## Goal

Get a React client talking to the Fleet API properly, once, so that every page after it is a
variation rather than a fresh argument. "Properly" means three things that are missing from most
first attempts: the network layer is one module rather than `fetch` scattered through components,
server data is cached by something that understands it is server data, and every page renders its
failure and empty states as deliberately as its happy path.

The scaffold already runs. `ClientApp/src/pages/DepotsPage.tsx` is the worked example - it fetches
`/api/depots`, and it is about thirty lines including the comments. Read it before you start;
this week's work is one more page, harder in exactly the ways a real list is harder.

Note what talks to what. In development Vite serves the SPA on `:5174` and proxies `/api` to your
API on `:5101`, stripping the prefix (`vite.config.ts`). The BFF host in `src/Fleet.Web.Workshop/`
exists but is not in the picture yet - it is week 2's subject, and this week nothing is
authenticated. Because of that, every fetch must use a **relative** path. An absolute
`http://localhost:5101/vehicles` will work on your laptop and break the moment anything is
deployed, and it is the single most common thing to have to unpick later.

## What to build

In `src/Fleet.Web.Workshop/ClientApp`:

- **The `Vehicle` contract**, in `src/api/contracts.ts`. It is already written for you - read it
  against a real response (`curl "http://localhost:5101/vehicles?page=1&pageSize=2"`) and satisfy
  yourself it is honest, including that `type` and `status` come back as numbers.
- **`VehiclesPage`** (`src/pages/VehiclesPage.tsx`), rendering one page of vehicles through
  `vehiclesApi.list()` from `src/api/clients/vehicles.ts`. It must render all four states:
  pending, error, empty, and the list. `DepotsPage` shows the shape; the error branch must display
  the message from `ApiError`, which the wrapper has already lifted out of the API's RFC 9457
  problem document - not "something went wrong".
- **Paging**, with the page number in the **query string** rather than in React state. A user who
  reloads on page 7, or sends someone the link, must land on page 7. `useSearchParams` from
  `react-router` reads and writes it. Use the `hasNextPage` and `totalPages` the API already sends
  rather than deriving them from `totalCount` yourself - the server has already decided where the
  last page is, and a client that recalculates it will eventually disagree.
- **A readable list.** `type` and `status` are numbers on the wire; a table showing `1` and `2` is
  not finished. Map them to labels somewhere sensible and be ready to defend where.
- **One sorted column or one filter** - `sort` and `status` are both understood by
  `GET /vehicles`; pick one. The point is that it goes in the query string next to `page`, and
  that changing it resets the page number, because page 7 of a different filter is meaningless.

**Do not add:** a component library, a state manager beyond React Query, authentication or a login
page, forms or mutations, or tests beyond confirming the page by hand. Week 2 is authentication and
it will change how `/api` is reached; the rest have their own weeks. Adding them now makes every
PR different in ways that have nothing to do with what is being taught.

## What to hand in

A pull request from your own repository (created from the template), containing:

- `VehiclesPage` with its four states, query-string paging, readable labels, and one sort or
  filter control.
- A short PR description answering two questions. First: where did you put the number-to-label
  mapping for `type` and `status`, and would you argue the API should have sent strings instead?
  Second: what is in your React Query `queryKey`, and what happens if the page number is left out
  of it?
- Evidence it works: screenshots or recordings of at least four states - the list on page 1, the
  same list on a later page with the URL visible, the error state with the API stopped, and an
  empty result (a filter that matches nothing).

**Reviewer checklist:**
- [ ] `npm run build` and `npm run lint` both pass
- [ ] Every fetch uses a relative path; no `http://localhost:5101` anywhere in `src/`
- [ ] Reloading the page on page 3 stays on page 3
- [ ] The error state shows the API's own message, not a status code or a generic string
- [ ] The empty state is a sentence, not a blank area
- [ ] The page number is part of the React Query key
- [ ] Changing the filter or sort resets the page number
- [ ] No `fetch` call outside `src/api/`
