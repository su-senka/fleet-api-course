# Week 1 - HTTP, REST and your first endpoint

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Endpoint stub   src/Fleet.Api.Workshop/Endpoints/DepotEndpoints.cs
    Module contract IDepotService
    Reference       src/Fleet.Api/ implements Vehicles and Bookings in full

  Everything the students call is finished, tested and seeded. The assignment is the HTTP layer
  over it, and the decisions that layer has to make.
-->

## Goal

Ship one working HTTP endpoint pair end to end: a request comes in, gets routed and bound, calls
a finished module, and comes back out as a status code you chose on purpose — not whatever the
framework defaulted to. The business logic already works and is already tested. This week is
only about the layer that talks to the outside world.

`IDepotService` is read-only — `ListAsync` and `GetAsync`, nothing that creates or changes a
depot. This resource does have a finished reference, `MapDepotEndpoints` in
`src/Fleet.Api/Endpoints/VehicleEndpoints.cs` (mapped from `src/Fleet.Api/Program.cs`) — but write
your own version first anyway; comparing the two afterwards is worth more than reading it going
in, and from week 2 onward most resources work the same way.

## What to build

In `Fleet.Api.Workshop`, add depot endpoints grouped under `/depots`:

- **`GET /depots`** - return every depot. No paging, filtering or sorting yet - that arrives in
  week 3, on a different resource. A flat JSON array is fine.
- **`GET /depots/{depotId}`** - return one depot, or the right status code when the id is
  well-formed but no such depot exists.

Both call `IDepotService`. Check its actual method names and the shape of `Result<T>` in
`Fleet.Modules.Vehicles.Contracts` yourself before you start - this stub doesn't restate them,
because they can change without this document being updated.

Map every outcome to a status code deliberately:

- success -> `200`
- anything the service reports as not found -> `404`
- anything unexpected -> never let this reach the client as a bare `500` with a stack trace.
  Catch it at the boundary and return a plain error body - it doesn't need the full
  `ProblemDetails` shape yet, that's week 6.

**Do not add:** authentication, a validation library, response caching, or automated tests
beyond confirming the endpoint by hand. Each of those gets its own week later. Doing them now
just makes it harder to compare everyone's PRs against the same bar.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The two endpoints, defined in one file via `MapGroup("/depots")`.
- A short PR description stating which DI lifetime your `IDepotService` dependency resolved as,
  and why that's the correct lifetime for it.
- Evidence it works: paste two request/response pairs into the PR description - one success
  path, one failure path - from curl or a `.http` file.

**Reviewer checklist:**
- [ ] Builds and runs against the Compose stack
- [ ] `GET /depots/{depotId}` returns `404`, not an exception, for an unknown but well-formed id
- [ ] A bad request never surfaces a raw exception or stack trace
- [ ] PR description states and justifies the DI lifetime
