# Week 8 - Authorization: roles, policies and the resource itself

<!--
  A stub. The specification for this week is written by the course author.

  What is already true, and does not need restating here:
    Endpoint stub   src/Fleet.Api.Workshop/Endpoints/BookingEndpoints.cs
    Module contract IBookingService, IDriverDirectory
    Reference       src/Fleet.Api/ implements Vehicles and Bookings in full

  Everything the students call is finished, tested and seeded. The assignment is the HTTP layer
  over it, and the decisions that layer has to make.
-->

## Goal

Week 7 ended with an API that knows who is asking. This week it decides what that person may have.
Three mechanisms do it, they are not interchangeable, and the point of the week is knowing which
question each one can answer:

1. **A role** is a fact about the caller. `fleet.driver` is true of them at every endpoint.
2. **A policy** is a named rule over those facts, applied to an endpoint. "Only an admin or a
   dispatcher may call this" is decidable from the token alone, before anything is loaded.
3. **The resource itself** is the rule a policy cannot express. "A driver may read *their own*
   booking" depends on the booking, so it cannot be answered until the booking is in hand -
   `[Authorize(Roles = "fleet.driver")]` can say a driver may call the endpoint, but never that
   they may not call it *for that row*.

There is a fourth thing that looks like the third and is not, and confusing them is the most
expensive mistake available this week. Keeping a driver to their own *single* booking is a
resource check. Keeping a driver to their own *list* is not - there is no single resource to check,
and filtering rows out after the query has run still leaks the true total through the paging
metadata. The list is fixed before the query, by pinning the filter.

The bridge from a token to a row is `IDriverDirectory.FindByUserIdAsync`
(`Fleet.Modules.Drivers.Contracts`), which takes the Keycloak username your week-7 claim mapping
put on `preferred_username` and returns a `DriverSummary?`. It is deliberately in Drivers rather
than in Bookings: Drivers owns the mapping between a login and a driver row. Three seeded drivers
have logins - `driver.dvorak`, `driver.cerna` and `driver.prochazka` - so signing in as any of
them puts you on a real row with real bookings.

This week has a finished counterpart, and it is worth reading only afterwards:
`Fleet.Api/Auth/BookingAuthorization.cs` and the endpoints in `Fleet.Api/Endpoints/BookingEndpoints.cs`.

## What to build

In `Fleet.Api.Workshop`:

- **Role and policy constants**, and the policies themselves, registered where the `TODO(week-8)`
  comment sits in `Program.cs`. At minimum a policy meaning "any authenticated member of the
  fleet" - authenticated, and holding any one of the three realm roles. Put it on the `/bookings`
  group in place of week 7's bare `RequireAuthorization()`. Named policies rather than role
  strings sprayed through the endpoints: when "who may cancel a booking" changes, it should change
  in one place. This is also the first moment your week-7 role flattening is load-bearing - if the
  realm roles never became role claims, every request now `403`s.
- **A resource-based requirement and handler** for one booking: a requirement type, an
  `AuthorizationHandler<TRequirement, BookingDto>`, and a registration as `IAuthorizationHandler`
  in `Program.cs`. The handler decides:
  - an admin or a dispatcher manages the whole fleet's calendar, so the question does not arise;
  - a driver succeeds only when the booking's `DriverId` is their own;
  - a caller with the driver role but no matching row in the directory **fails closed**. A token
    that claims to be a driver of nothing is not a reason to grant access.

  Call it from `GET /bookings/{bookingId:guid}` through `IAuthorizationService.AuthorizeAsync`,
  after loading the booking and before returning it.
- **The list, pinned.** In `GET /bookings`, resolve the caller's own driver id - `null` for admins
  and dispatchers, who are not restricted to anyone's bookings - and when there is one, force the
  `driverId` filter term to it before calling `ListAsync`. Overwrite whatever the client sent in
  the query string; do not merge with it, and do not trust it. `IBookingService.ListAsync` already
  understands a `driverId` term, so this is a filter change, not a new query.
- **`POST /bookings`**, over `IBookingService.BookAsync`. A driver may book for themselves and for
  nobody else; a dispatcher or admin may book for anyone. Return `403` with a distinct error code
  when a driver names someone else's `DriverId` - and note that this is a different failure from
  the module's own `Conflict` when a driver has no valid licence, which stays a `409`. Being
  allowed to ask and the world being able to answer are separate things. Reuse your week-5
  validation filter for the request body if you have one; it is not what is being assessed here.
- **A decision, stated in the PR:** when a driver asks for someone else's booking, is that `403`
  or `404`? Both are defensible. `404` hides whether the booking exists, which matters when ids
  are guessable and existence is itself sensitive; `403` is honest and tells a confused user
  something true. The reference picks one and gives its reason in a comment - pick yours before
  you read it.

**Do not add:** `ETag`/`If-Match` handling or the reschedule and cancel endpoints (week 9),
`Idempotency-Key` (week 10), or authorization on `/vehicles`, `/drivers` and `/certificates` -
those resources are still open on purpose and locking them down now makes every earlier week's
evidence stop reproducing.

## What to hand in

A pull request from your own repository (created from the template), containing:

- The policies, the resource requirement and handler with its registration, the pinned list, and
  `POST /bookings`.
- A short PR description explaining which of the three mechanisms you used for each rule and why
  the list could not use the same one as the single booking. State your `403`-versus-`404` choice
  and its cost. If you defined a policy you did not end up applying anywhere, say what it is for -
  the reference has one of those, and an unused policy is either a plan or a mistake.
- Evidence it works: paste at least five request/response pairs into the PR description, using
  tokens from `requests/auth.http` -
  - `GET /bookings` as `driver.dvorak`, showing only their own bookings;
  - the same request as `dispatch.svoboda`, showing a larger total in the paging metadata;
  - `GET /bookings/{id}` as a driver, for a booking belonging to a different driver, showing your
    chosen status code;
  - the same id as an admin or dispatcher, showing `200`;
  - `POST /bookings` as `driver.dvorak` with someone else's `DriverId`, showing `403`.

**Reviewer checklist:**
- [ ] Builds and runs against the Compose stack
- [ ] A driver's `GET /bookings` is filtered before the query, not after - the paging total
      reflects only their own rows
- [ ] A `driverId` in the query string cannot widen what a driver sees
- [ ] A driver reading another driver's booking is refused; an admin or dispatcher is not
- [ ] The driver-role-with-no-matching-row case fails closed
- [ ] A driver cannot `POST` a booking naming another driver
- [ ] A driver without a valid licence still comes back as `409`, not `403`
- [ ] PR description maps each rule to its mechanism and justifies the `403`/`404` choice
