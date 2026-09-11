# Week 3 - Collections: paging, filtering and sorting

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Endpoint stub   src/Fleet.Api.Workshop/Endpoints/VehicleEndpoints.cs
    Module contract IVehicleService
    Reference       src/Fleet.Api/ implements Vehicles and Bookings in full

  Everything the students call is finished, tested and seeded. The assignment is the HTTP layer
  over it, and the decisions that layer has to make.
-->

## Goal

Turn last week's flat `GET /vehicles` into a real collection endpoint: paged, filterable and
sortable, on top of a service method that already does all three. The seed has 250 vehicles —
enough that returning them all in one response is visibly the wrong answer, and enough that a
client actually needs a page, a filter and a sort to find the thing it wants.

`IVehicleService` has two `ListAsync` overloads. The unpaged one — `ListAsync(CancellationToken)`
— is what week 2 called; it stays in the contract only because `PageRequest.MaxPageSize` caps a
single page at 100, fewer than the seeded 250, so it cannot be replaced by the paged overload in
one call. This week's endpoint switches to the other one:
`ListAsync(PageRequest, SortRequest?, FilterRequest, CancellationToken)`, in
`Fleet.Modules.Vehicles.Contracts`. Read its XML doc comment before you start — it states exactly
which filter terms (`status`, `type`, `depotId`, plus free-text `Search` matched against the
plate) and which sort fields (`plate`, `odometerKm`, `status`, `type`) the service understands,
and that an unsupported sort field is reported back as `ErrorKind.Validation` rather than silently
ignored.

`Fleet.Common.Paging` has the three request types this method takes — `PageRequest`,
`SortRequest`, `FilterRequest` — plus `PagedResult<T>` for the response. Building them from the
query string is this week's job; nothing in `Fleet.Api.Workshop` does it for you yet. Check each
type's own doc comments for how it wants to be constructed and what it clamps versus rejects
before you write the parsing code.

This resource has a finished reference, `MapVehicleEndpoints` in
`src/Fleet.Api/Endpoints/VehicleEndpoints.cs` (see also `PagingParameters` in
`src/Fleet.Api/Http/`) — write your own version first anyway; comparing the two afterwards is
worth more than reading it going in.

## What to build

In `Fleet.Api.Workshop`, replace `ListAsync` in `VehicleEndpoints.cs` (still grouped under
`/vehicles`) so that `GET /vehicles`:

- Reads paging from `page` and `pageSize` query parameters and builds a `PageRequest` with
  `PageRequest.Of`. Decide yourself whether an out-of-range value should be clamped or rejected —
  `PageRequest.Of` clamps; you don't have to follow it, but you should know which you chose and
  why.
- Reads a `sort` query parameter and builds a `SortRequest?` with `SortRequest.Parse` — `plate`,
  `-plate` and `plate:desc` are all valid forms; a missing parameter means "no sort requested."
- Reads any remaining query parameters into a `FilterRequest`, with `q` as the free-text search
  and everything else as a named term. Do not hardcode the three term names the service happens
  to understand — pass through what the client sent and let the service decide what it recognizes.
- Calls the paged `IVehicleService.ListAsync` overload with all three, and returns the
  `PagedResult<VehicleDto>` as the response body.
- Maps `ErrorKind.Validation` from an unsupported sort field to a status code on purpose — 400 and
  422 are both defensible; pick one and be ready to say why in the PR description.

Everything else about the endpoint (route, other status codes, no-auth) is unchanged from week 2.

**Do not add:** authentication, creating or changing a vehicle, response caching, a validation
library, or automated tests beyond confirming the endpoint by hand. Each of those is a later week.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The updated `GET /vehicles` handler.
- A short PR description stating which status code you chose for an unsupported sort field, and
  why.
- Evidence it works: paste at least three request/response pairs into the PR description — one
  plain paged list, one with a filter term applied, one with `sort` set to a field the service
  does not support — from curl or a `.http` file.

**Reviewer checklist:**
- [ ] Builds and runs against the Compose stack
- [ ] `GET /vehicles` returns a `PagedResult<VehicleDto>`, not a flat array
- [ ] `page`/`pageSize` reach the service as a `PageRequest`; out-of-range values are handled
      deliberately, not left to throw
- [ ] At least one filter term (`status`, `type`, `depotId` or `q`) is passed through and visibly
      narrows the results
- [ ] An unsupported `sort` field produces a deliberate status code, not an unhandled exception
- [ ] PR description states and justifies the status code chosen for that case
