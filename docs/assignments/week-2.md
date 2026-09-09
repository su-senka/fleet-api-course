# Week 2 - Resources, routing and status codes

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

Ship the read side of a second resource, this time one with enough shape to make routing and
status-code decisions actually matter: `GET /vehicles` and `GET /vehicles/{vehicleId}`. The
service behind them, `IVehicleService`, is finished and tested; this week is still only about the
layer that turns its `Result<T>` into a response.

`IVehicleService` has more on it than these two calls need — registering a vehicle, changing its
status, recording an odometer reading. Leave all of that alone; it belongs to weeks 3 and 4.
`ListAsync` also takes a page, a sort and a filter argument, because paging, filtering and sorting
are next week's job, not this one's — `Fleet.Common.Paging` has defaults built for exactly this
situation, so you don't need to read a query string yet to call it. Check the signature yourself
before you start; it doesn't get restated here because it can change without this document being
updated.

## What to build

In `Fleet.Api.Workshop`, add vehicle endpoints grouped under `/vehicles` (`MapGroup`, one file, as
in week 1):

- **`GET /vehicles`** - every vehicle, unpaged, as a flat JSON array. Paging arrives next week.
- **`GET /vehicles/{vehicleId}`** - one vehicle, or the right status code when the id is
  well-formed but no such vehicle exists.

Both call `IVehicleService`. Route it deliberately: a route parameter constrained to `:guid` never
reaches your handler with a malformed value, which changes what "bad id" even means for this
endpoint compared to a route that accepts any string and tries to parse it itself. Pick one and
know why.

Map every outcome to a status code on purpose:

- success -> `200`
- the service reporting not-found -> `404`
- anything unexpected -> never a bare `500` with a stack trace; catch it at the boundary and
  return a plain error body, same as week 1.

**Do not add:** authentication, paging/filtering/sorting, creating or changing a vehicle,
response caching, a validation library, or automated tests beyond confirming the endpoint by
hand. Each of those is a later week, most of them on this same resource.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The two endpoints, defined in one file via `MapGroup("/vehicles")`.
- A short PR description stating which DI lifetime your `IVehicleService` dependency resolved as,
  and why.
- Evidence it works: paste two request/response pairs into the PR description - one success
  path, one failure path - from curl or a `.http` file.

**Reviewer checklist:**
- [ ] Builds and runs against the Compose stack
- [ ] `GET /vehicles/{vehicleId}` returns `404`, not an exception, for an unknown but well-formed id
- [ ] A malformed id is handled deliberately, not by an unhandled parse exception
- [ ] `GET /vehicles` returns the full unpaged list - no paging, filtering or sorting yet
- [ ] PR description states and justifies the DI lifetime
