# Week 4 - Creating and changing resources

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Endpoint stub   src/Fleet.Api.Workshop/Endpoints/VehicleEndpoints.cs
    Module contract IVehicleService, IDriverService
    Reference       src/Fleet.Api/ implements Vehicles and Bookings in full

  Everything the students call is finished, tested and seeded. The assignment is the HTTP layer
  over it, and the decisions that layer has to make.
-->

## Goal

Ship the write side, on two resources at once: finish Vehicles with `POST /vehicles` and
`PUT /vehicles/{vehicleId}/status`, and build Drivers from scratch with `GET /drivers`,
`GET /drivers/{driverId}` and `POST /drivers`. Every earlier week only read; this week your
handler also decides what request body is worth accepting and what happens when the module
refuses it.

Both services report the same two write failures in different shapes. Registering a vehicle
fails with `ErrorKind.NotFound` when `DepotId` names no depot, and `ErrorKind.Conflict` when the
plate is already taken; registering a driver fails with `ErrorKind.Conflict` when the employee
number is already taken. Moving a vehicle's status is not a free transition either — retiring a
vehicle and then trying to move it again also comes back `ErrorKind.Conflict`, not something you
need to check for yourself before calling `ChangeStatusAsync`. None of this is optional error
handling; it is the module telling you exactly which status code the request earned.

Vehicles has a finished reference this week: `RegisterAsync` and `ChangeStatusAsync` in
`MapVehicleEndpoints`, `src/Fleet.Api/Endpoints/VehicleEndpoints.cs`. Write your own version
first anyway. Drivers does not — there is no `DriverEndpoints` anywhere under `src/Fleet.Api/`,
so this resource is yours from a blank file, shaped after however you just wrote Vehicles.

## What to build

In `Fleet.Api.Workshop`:

**`VehicleEndpoints.cs`** (still grouped under `/vehicles`), add:

- **`POST /vehicles`** — reads a request body, maps it to a `RegisterVehicleCommand` (`Plate`,
  `Type`, `Guid DepotId`, `int OdometerKm`), and calls `IVehicleService.RegisterAsync`. A command
  is not a request body — do that mapping yourself in the endpoint rather than binding the
  request straight to `RegisterVehicleCommand`, so a change to your JSON shape never has to touch
  the module. On success, this creates something: respond `201`, with a `Location` header
  pointing at the new vehicle (`Results.CreatedAtRoute` against your `GetVehicle` route works, if
  you named it). `ErrorKind.NotFound` (unknown depot) and `ErrorKind.Conflict` (plate taken) both
  come back from the same call — map each to its own status code, not one generic failure
  response.
- **`PUT /vehicles/{vehicleId}/status`** — reads a request body carrying the new
  `VehicleStatus`, and calls `IVehicleService.ChangeStatusAsync(vehicleId, status, ...)`. This is
  a `PUT`, not a `PATCH`: the status is the entire resource being replaced at that sub-route, so
  there is nothing partial to express. `ErrorKind.Conflict` on an illegal transition (retired
  vehicles do not come back) is a `409`, same as the plate conflict above.

**`DriverEndpoints.cs`** — currently an empty stub with no routes mapped at all. Add, grouped
under `/drivers`:

- **`GET /drivers`** and **`GET /drivers/{driverId}`** — same shape as `GetVehicle`/`ListVehicles`
  from week 2: call `IDriverService.ListAsync`/`GetAsync`, map `ErrorKind.NotFound` to `404`.
  `ListAsync` already takes paging, sorting and filtering, same as `IVehicleService` — reuse
  whatever you built for that in week 3 rather than writing it twice.
- **`POST /drivers`** — maps a request body to `RegisterDriverCommand` (`EmployeeNumber`, `Name`,
  `string? UserId`), calls `IDriverService.RegisterAsync`, returns `201` with `Location` on
  success, `409` on `ErrorKind.Conflict` (employee number taken).

**Do not add:** authentication, a validation library, response caching, certificates, or
automated tests beyond confirming the endpoints by hand. Validation gets its own week next.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The two new Vehicles endpoints and the three new Drivers endpoints.
- A short PR description stating, for one of the two resources, which status code you chose for
  each of its write failures and why.
- Evidence it works: paste at least four request/response pairs into the PR description —
  one successful `POST /vehicles`, one `POST /vehicles` that hits a conflict, one
  `PUT /vehicles/{vehicleId}/status`, and one `POST /drivers` — from curl or a `.http` file.

**Reviewer checklist:**
- [ ] Builds and runs against the Compose stack
- [ ] A successful `POST` returns `201` with a `Location` header, not `200`
- [ ] `POST /vehicles` distinguishes an unknown depot (`404`) from a taken plate (`409`)
- [ ] `PUT /vehicles/{vehicleId}/status` returns `409` for an illegal transition, not `400`
- [ ] `POST /drivers` returns `409` for a duplicate employee number
- [ ] The request body is mapped to a command in the endpoint, not bound directly to it
- [ ] PR description states and justifies the status codes chosen for the write failures
