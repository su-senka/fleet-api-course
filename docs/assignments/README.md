# Weekly assignments

Fourteen stubs, titles only. **The specifications are written by the course author** - a good
assignment encodes what the last group struggled with, and this repository does not know that yet.

The titles below are a proposed arc, not a fixed one. Two things constrain it: week 12 is named in
the code, by the `TODO(week-12)` at the supplier client's registration site, and each endpoint stub
in `src/Fleet.Api.Workshop/Endpoints/` points at the week that fills it in. Reordering the weeks
means updating those markers.

| Week | Title | Resource | Module contract |
|---|---|---|---|
| 1 | HTTP, REST and your first endpoint | Depots | `IDepotService` |
| 2 | Resources, routing and status codes | Vehicles | `IVehicleService` |
| 3 | Collections: paging, filtering and sorting | Vehicles | `IVehicleService` |
| 4 | Creating and changing resources | Vehicles, Drivers | `IVehicleService`, `IDriverService` |
| 5 | Validation and error responses | Drivers | `IDriverService` |
| 6 | Sub-resources, and serving something that is not JSON | Certificates | `IDriverService`, `IBlobStore` |
| 7 | Authentication with JWT bearer tokens | Bookings | `IBookingService` |
| 8 | Authorization: roles, policies and the resource itself | Bookings | `IDriverDirectory` |
| 9 | Concurrency: ETags and If-Match | Bookings | `IBookingService` |
| 10 | Idempotency and safe retries | Bookings | `IIdempotencyStore` |
| 11 | Caching, and what it is safe to cache | Work orders | `IMaintenanceService` |
| 12 | Resilience: timeouts, retries and circuit breakers | Work orders | `IMaintenanceService` |
| 13 | Long-running work: 202 Accepted and polling | Reports | `IReportService` |
| 14 | Documentation, versioning, and a look back | Notifications | `INotificationService` |

## What the repository already provides for each week

Every module is finished, tested and seeded before week 1. An assignment never asks for business
logic; it asks for the HTTP layer over logic that already works, and for the decisions that layer
has to make.

Three weeks have something in the repository that exists specifically to make them concrete:

- **Week 9** - `Booking.RowVersion` maps Postgres' `xmin`, and the DTO exposes it base64-encoded,
  ready to become an `ETag`. See ADR 5.
- **Week 10** - `IIdempotencyStore` is finished and its atomicity is tested with twenty parallel
  claims on one key. The middleware that uses it is not written.
- **Week 12** - `Supplier.Api.Fake` fails a quarter of requests, stalls a sixth of them for eight
  seconds, and rate-limits after twenty a minute, all reproducibly from `FAKE_SEED`. The typed
  client that calls it has no timeout, no retry and no circuit breaker, and says so.

Weeks 2, 3, 4, 7, 8, 9 and 10 all have a finished counterpart in `src/Fleet.Api/` that students can
read afterwards. Weeks 1, 5, 6, 11, 12, 13 and 14 do not - those resources have no reference
implementation anywhere, by design.
