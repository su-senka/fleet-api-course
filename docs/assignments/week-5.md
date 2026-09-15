# Week 5 - Validation and error responses

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Endpoint stub   src/Fleet.Api.Workshop/Endpoints/DriverEndpoints.cs
    Module contract IDriverService
    Reference       src/Fleet.Api/ implements Vehicles and Bookings in full

  Everything the students call is finished, tested and seeded. The assignment is the HTTP layer
  over it, and the decisions that layer has to make.
-->

## Goal

Reject a malformed `POST /drivers` request before it reaches `IDriverService`, and report
everything wrong with it in one response instead of one field at a time. `Driver.Register`
already checks its own invariants - a required employee number, a required name, both bounded in
length (`Driver.EmployeeNumberMaxLength` is 20, `Driver.NameMaxLength` is 120, both in
`Fleet.Modules.Drivers/Domain/Driver.cs`) - but it does so like an ordinary method: it returns on
the *first* rule it finds broken. Send a request with both fields empty today, and you are told
only about the employee number; fix that, resubmit, and only then learn the name was empty too.
This week is about catching every shape problem in one pass, at the HTTP boundary, before the
module is even called - and about not duplicating a rule the module already enforces.

`Fleet.Api.Workshop` already has the pieces `FluentValidation` needs except the wiring:
`FluentValidation` and `FluentValidation.DependencyInjectionExtensions` are referenced in its
`.csproj`, and `Program.cs` has a commented-out `TODO(week-5)` block showing the one line that
registers validators - read the comment above it, `includeInternalTypes` is not optional, or a
validator silently never runs. What Workshop does *not* have yet is an endpoint filter that calls
those validators: `Fleet.Api/Http/ValidationFilter.cs` is that filter for the reference host, and
`Fleet.Api/Validation/VehicleRequestValidators.cs` shows what a validator for it looks like
(`RegisterVehicleRequestValidator`, checking exactly the kind of thing described above and nothing
the database would have to answer). There is no `DriverRequestValidator` anywhere in `src/Fleet.Api`
to compare against afterwards - Drivers has no reference implementation this week, same as weeks
1, 6 and 11-14.

## What to build

In `Fleet.Api.Workshop`:

- An endpoint filter that runs a `FluentValidation` validator over one handler argument and turns
  a failure into the same RFC 9457 shape every other error already takes. `ProblemResults.From` in
  `Fleet.Api.Workshop/Http/ProblemResults.cs` already promotes `Error.Details` into the `errors`
  extension - reuse it rather than building a second response shape. `Error.Validation(code,
  message, details)` (`Fleet.Common.Results.Error`) is the overload that carries a per-field
  dictionary; build one keyed by property name from the validator's failures.
- A validator for `RegisterDriverRequest` (`Fleet.Api.Workshop/Requests/DriverRequests.cs`)
  checking exactly what `Driver.Register` checks: `EmployeeNumber` required and at most 20
  characters, `Name` required and at most 120 characters. **Do not** check that the employee
  number is unique - that needs the database, `IDriverService.RegisterAsync` already does it, and
  it correctly comes back as `ErrorKind.Conflict`, not `Validation`. Duplicating it here would
  produce two implementations of one rule, and a 409 the validator now races to also call a 400.
- The registration call from the `TODO(week-5)` comment in `Program.cs`, uncommented and adapted.
- The filter wired onto `POST /drivers` in `DriverEndpoints.cs`, the same way
  `Fleet.Api/Endpoints/VehicleEndpoints.cs` wires it onto `RegisterVehicleRequest`.

**Do not add:** validation on `GET /drivers`'s paging or filter parameters - the unsupported-sort
422 there is already handled from week 3, and query-string validation is a different shape of
problem from a request body's. Do not touch `Driver.Register`'s own checks either; the point is a
second, earlier layer, not a replacement for the first.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The endpoint filter, the `RegisterDriverRequest` validator, the `Program.cs` registration, and
  `POST /drivers` wired to use it.
- A short PR description stating which of `Driver.Register`'s checks you duplicated in the
  validator and which one you deliberately left for the module to enforce, and why.
- Evidence it works: paste at least three request/response pairs into the PR description - one
  `POST /drivers` with both `EmployeeNumber` and `Name` empty, showing both field errors in a
  single response body; one with a well-formed body but an employee number that already exists,
  showing it still comes back as `409` rather than `400`; and one successful `POST`.

**Reviewer checklist:**
- [ ] Builds and runs against the Compose stack
- [ ] A request missing both `EmployeeNumber` and `Name` returns one response naming both fields,
      not two separate round trips
- [ ] Validators are registered with `includeInternalTypes: true`
- [ ] A duplicate employee number still returns `409`, not `400` - the validator does not
      re-implement the uniqueness check
- [ ] The validation failure response uses the same RFC 9457 shape as every other error
- [ ] PR description states which check lives in the validator and which stays in the module, and
      why
