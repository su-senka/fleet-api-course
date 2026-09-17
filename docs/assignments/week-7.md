# Week 7 - Authentication with JWT bearer tokens

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Endpoint stub   src/Fleet.Api.Workshop/Endpoints/BookingEndpoints.cs
    Module contract IBookingService
    Reference       src/Fleet.Api/ implements Vehicles and Bookings in full

  Everything the students call is finished, tested and seeded. The assignment is the HTTP layer
  over it, and the decisions that layer has to make.
-->

## Goal

Put the Bookings endpoints behind a real identity. Until now every request to your host has been
anonymous and the API had no opinion about it; from this week on, `GET /bookings` without a
credential is a `401` and with a valid Keycloak token is a `200`. The question *which* bookings
that caller may see is week 8's - this week ends the moment the API can say who is asking, and
say it from a token it verified rather than from anything the client asserted about itself.

Keycloak is already running in Compose with the `fleet` realm, six users and three roles
(`fleet.admin`, `fleet.dispatcher`, `fleet.driver` - see `infra/keycloak/realm-export.json`), and
`requests/auth.http` already has the three token requests. `Fleet.Api.Workshop/appsettings.json`
already carries `Authentication:Authority` (`http://localhost:8080/realms/fleet`) and
`Authentication:Audience` (`fleet-api`), so there is no configuration to invent - read those two
keys rather than hard-coding either.

What is *not* already there, and is different from week 5: the JWT bearer package is not
referenced by `Fleet.Api.Workshop.csproj`. You add it yourself. The version is pinned centrally
in `Directory.Packages.props`, so the reference goes in without one:
`<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />`.

This week has a finished counterpart: `Fleet.Api/Auth/AuthenticationExtensions.cs`. Write yours
first. There is one thing in that file - the shape Keycloak gives its realm roles - that costs
everyone an hour if they meet it by accident in week 8 instead of on purpose now.

## What to build

In `Fleet.Api.Workshop`:

- The package reference above, in `Fleet.Api.Workshop.csproj`.
- An `AddFleetAuthentication` extension - the `TODO(week-7)` comment in `Program.cs` already names
  the call, so put it where that comment is. It registers JWT bearer against the configured
  authority and audience, and it must validate issuer, audience, lifetime and signing key. Two
  details are worth deciding rather than defaulting:
  - `RequireHttpsMetadata` - Keycloak in Compose speaks plain HTTP, so this has to be false
    locally. Guard it on the environment; a host that disables it in production is a security bug,
    not a convenience.
  - `ClockSkew` - the default is five minutes, which hides expiry bugs for the whole of a workshop
    session. Consider shortening it and say why in your PR.
- **Claim mapping**, which is the part that is really about Keycloak rather than about JWT. Set
  `NameClaimType` to `preferred_username` - `sub` is an opaque GUID and `name` is a display name
  that is not unique - and set `RoleClaimType` to `ClaimTypes.Role`. Then handle the realm roles:
  Keycloak nests them as `"realm_access": { "roles": [...] }`, ASP.NET Core turns that whole JSON
  object into the value of one claim, and `User.IsInRole("fleet.driver")` is therefore `false` no
  matter who signs in. Flatten them into ordinary role claims in an `OnTokenValidated` event.
  Nothing this week checks a role, so nothing this week will fail if you skip it - which is
  exactly why it is worth doing now, with a decoded token in front of you, rather than debugging
  it through a `403` next week.
- `app.UseAuthentication()` and `app.UseAuthorization()`, and `AddAuthorization()` on the service
  side. **Note that the `TODO(week-8)` marker on `app.UseAuthorization()` in `Program.cs` is
  misleading**: week 8 adds the *policies*, but the middleware and its services are needed as soon
  as an endpoint carries `RequireAuthorization()`, which is this week. Without it the framework
  throws on the first matched request rather than quietly letting it through - read the message,
  it tells you exactly this.
- In `Endpoints/BookingEndpoints.cs`, a `/bookings` group with `RequireAuthorization()` on the
  group - not on each endpoint - and two read endpoints over `IBookingService`
  (`Fleet.Modules.Bookings.Contracts`):
  - **`GET /bookings`** - one page, through `ListAsync`, reading page, sort and filter from the
    query string exactly as `/vehicles` and `/drivers` already do.
  - **`GET /bookings/{bookingId:guid}`** - one booking through `GetAsync`, `404` when there is no
    such booking.
- `app.MapBookingEndpoints()`, uncommented in `Program.cs`.

**Do not add:** policies or role requirements (`RequireAuthorization()` with no argument is the
whole of this week's authorization), the rule that a driver sees only their own bookings, the
`POST`/`PUT`/`DELETE` endpoints, an `ETag`, or `Idempotency-Key` handling. Those are weeks 8, 9
and 10, and each of them assumes this week works.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The package reference, `AddFleetAuthentication`, the two middleware calls, and the two `GET`
  endpoints behind `RequireAuthorization()`.
- A short PR description stating what your token validation actually checks and why
  `RequireHttpsMetadata` is safe to relax here and nowhere else. Paste the decoded `realm_access`
  claim from a real token and explain, in a sentence, why `User.IsInRole("fleet.driver")` would be
  `false` without your flattening step.
- Evidence it works: paste at least three request/response pairs into the PR description - one
  `GET /bookings` with no `Authorization` header showing `401`; one with a token from
  `requests/auth.http` showing `200`; and one `GET /bookings/{id}` for an id that does not exist,
  with a valid token, showing `404` rather than `401` or a stack trace.

**Reviewer checklist:**
- [ ] Builds and runs against the Compose stack
- [ ] `GET /bookings` without a token is `401`; with a valid token it is `200`
- [ ] Authority and audience come from configuration, not from a string literal in code
- [ ] `RequireHttpsMetadata` is relaxed only in Development
- [ ] Keycloak's nested realm roles are flattened into role claims, and the PR explains why
- [ ] `RequireAuthorization()` is on the group rather than repeated per endpoint
- [ ] An unknown booking id with a valid token returns `404`, not `401`
