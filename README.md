# Fleet

A teaching repository for a 14-week course on web APIs and REST in .NET.

**Every module in this repository is finished. The HTTP layer is not.** The domain rules,
application services, EF Core mappings, migrations, unit tests and seed data are all written and
passing. What is missing is the API on top, and writing it is the course.

Two hosts run side by side:

| Host | Port | What is in it |
|---|---|---|
| `Fleet.Api` | 5100 | The reference slice. Vehicles and Bookings, implemented in full. Read it. |
| `Fleet.Api.Workshop` | 5101 | Yours. Same modules, same wiring, zero endpoints. Write it. |

Everything else — six modules, 3,000 lines of tested domain logic, deterministic seed data, a
transactional outbox, a blob store, an idempotency store and a deliberately unreliable upstream —
is finished before week 1.

---

## Prerequisites

- **.NET 10 SDK** — check with `dotnet --list-sdks`; you want `10.0.4xx`.
- **Docker Desktop** (or Docker Engine + Compose v2). Give it at least 4 GB of memory;
  six containers is not much, but Keycloak alone will take 1 GB.
- **make** — optional. Every target is a one-line `docker compose` or `dotnet` command, and the
  PowerShell equivalents are listed at the bottom of this file.

## Getting started

```bash
docker compose up -d --wait     # or: make up
dotnet build Fleet.sln
dotnet test Fleet.sln
dotnet run --project src/Fleet.Api -- --seed   # migrate and load the seed data, then exit
dotnet run --project src/Fleet.Api             # reference API on :5100
dotnet run --project src/Fleet.Api.Workshop    # your API on :5101
```

Either host migrates and seeds on startup in Development, so the explicit `--seed` run is only
needed when you want a populated database without leaving an API running. Set
`Database:Initialize` to `false` to start a host that leaves the database alone.

`--wait` blocks until every container reports healthy, which takes about a minute on a cold
start — mostly Keycloak importing the realm.

## Running both hosts side by side

This is how the repository is meant to be used. Two terminals:

```bash
dotnet run --project src/Fleet.Api             # the reference, on :5100
dotnet run --project src/Fleet.Api.Workshop    # yours, on :5101
```

Then open both:

| | |
|---|---|
| <http://localhost:5100/scalar> | thirteen endpoints across Vehicles, Depots and Bookings |
| <http://localhost:5101/scalar> | **empty**, and filling it in is the course |

They share one database and register exactly the same six modules. The only difference is that one
has endpoints. Everything you need to write them is already running — 250 vehicles, 3,002 bookings,
Keycloak with six users, and a supplier that fails on purpose.

Start at [`docs/assignments/week-1.md`](docs/assignments/week-1.md), then open the matching stub in
`src/Fleet.Api.Workshop/Endpoints/`. When you are stuck, the finished version of Vehicles and
Bookings is in `src/Fleet.Api/` — but try it first. Reading the answer before attempting the
question is a reliable way to feel like you understood something you did not.

> **macOS:** ports 5000 and 5001 are taken by the AirPlay Receiver, which is why this repository
> uses 5100 and 5101. If you would rather have 5000 back, turn AirPlay Receiver off in
> *System Settings → General → AirDrop & Handoff*.

## Ports

| Service | URL / host port | Credentials | Notes |
|---|---|---|---|
| Postgres | `localhost:5432` | `fleet` / `fleet`, database `fleet` | One schema per module |
| Seq (logs) | <http://localhost:8081> | none | Structured logs from both hosts |
| Jaeger (traces) | <http://localhost:16686> | none | Search for service `fleet-api` |
| Jaeger OTLP | `localhost:4317` (gRPC), `4318` (HTTP) | — | Where the APIs export traces |
| Keycloak | <http://localhost:8080> | `admin` / `admin` | Realm `fleet`, imported on first start |
| Keycloak health | `localhost:9000` | — | Management port, not for application traffic |
| Azurite (blobs) | `localhost:10000` | `UseDevelopmentStorage=true` | Certificate scans, generated reports |
| Supplier (fake) | <http://localhost:5080/health> | none | Deliberately unreliable, see below |
| `Fleet.Api` | <http://localhost:5100> | — | `dotnet run`, not in Compose |
| `Fleet.Api.Workshop` | <http://localhost:5101> | — | `dotnet run`, not in Compose |

The two APIs deliberately run on your machine rather than in Compose. You will be restarting them
every few minutes, and `dotnet watch` is faster than a container rebuild.

## Connection strings

Both hosts read these from `appsettings.json`, and the defaults match the Compose ports above:

```json
{
  "ConnectionStrings": {
    "Fleet": "Host=localhost;Port=5432;Database=fleet;Username=fleet;Password=fleet",
    "Blobs": "UseDevelopmentStorage=true"
  }
}
```

`UseDevelopmentStorage=true` is the well-known Azurite shorthand: the emulator's fixed account
name, key and ports. It is not a secret, and it will not work against real Azure.

## Getting a token from Keycloak

The realm has three roles — `fleet.admin`, `fleet.dispatcher`, `fleet.driver` — and six users, all
with the password `fleet`:

| Username | Role |
|---|---|
| `admin.novak` | `fleet.admin` |
| `dispatch.svoboda`, `dispatch.horak` | `fleet.dispatcher` |
| `driver.dvorak`, `driver.cerna`, `driver.prochazka` | `fleet.driver` |

The `fleet-api` client is public and has direct access grants enabled, so a password grant is
enough for local work:

```bash
curl -s -X POST http://localhost:8080/realms/fleet/protocol/openid-connect/token \
  -d client_id=fleet-api \
  -d username=driver.dvorak \
  -d password=fleet \
  -d grant_type=password | jq -r .access_token
```

Paste the result into <https://jwt.io> and look at the claims. Three matter later:

- `aud` is `fleet-api`, which is what the API validates the token against.
- `realm_access.roles` carries the `fleet.*` roles.
- `employee_number` is a custom claim on the driver accounts. It is how a resource-based
  authorization handler answers "is this token's owner the driver on this booking?" without the
  Bookings module having to know what Keycloak is.

A password grant with a public client is fine on a laptop and wrong everywhere else. Real clients
use the authorization code flow with PKCE.

## The fake supplier

`Supplier.Api.Fake` is an upstream that fails on purpose, so that the week-12 resilience
assignment has something real to defend against. Its behaviour is set by environment variables in
`docker-compose.yml`:

| Variable | Default | Effect |
|---|---|---|
| `FAKE_SEED` | `20260101` | Seeds the RNG, so failures repeat identically across restarts |
| `FAKE_ERROR_RATE` | `0.25` | Fraction of requests answered with `500` |
| `FAKE_SLOW_RATE` | `0.15` | Fraction that sleep before answering |
| `FAKE_SLOW_MS` | `8000` | How long "slow" is |
| `FAKE_RATE_LIMIT` | `20` | Requests per minute before `429` with `Retry-After` |
| `FAKE_OUTAGE_AFTER` | unset | After N requests, fail everything for 60 seconds |

Every decision it makes is logged with the request's sequence number, to the console and to Seq if
`SEQ_URL` is set - which it is in `docker-compose.yml`. Filter Seq by `Service = 'supplier-fake'`
and you can line the supplier's behaviour up against your own circuit breaker's, in one place.

**The failures are reproducible.** A shared `Random` would give a different sequence on every run,
because concurrent requests race for it. Instead each request takes a sequence number and its
treatment is derived by hashing `(FAKE_SEED, sequence)`, so request 37 gets the same verdict
whether it arrives alone or alongside twenty others. Same seed, same run, same failures. The rate
limit and the outage window are the exceptions: both are genuinely about wall-clock time.

`/health` is deliberately outside the failure injection - a container that reported itself
unhealthy a quarter of the time would be no use to `docker compose`.

To see it misbehave for yourself:

```bash
# 1000 orders with only the error rate turned on; expect roughly 250 failures
docker compose exec supplier-fake sh -c 'echo'   # just to check it is up
for i in $(seq 1 40); do
  curl -s -o /dev/null -w "%{http_code} " -X POST http://localhost:5080/orders \
    -H 'Content-Type: application/json' \
    -d '{"workOrderId":"00000000-0000-0000-0000-000000000001","lines":[{"partNumber":"OLEJ-FILTR","quantity":1}]}'
done; echo
```

The first twenty come back `201` or `500`; after that the rate limit kicks in and the rest are
`429` with a `Retry-After` header. Change `FAKE_ERROR_RATE` and friends in `docker-compose.yml`
and `docker compose up -d --force-recreate supplier-fake` to make it kinder or crueller.

## The seed data

`make reset` gives you the same database every time: same ids, same plates, same drivers. That
matters because a `.http` file with an id in it keeps working, and because two people comparing
screens are looking at the same rows.

| | Count | Worth knowing |
|---|---|---|
| Depots | 4 | Praha, Brno, Ostrava, Plzen |
| Vehicles | 250 | ~25 in maintenance and ~13 retired, so the status filter has something to find |
| Odometer readings | 15,000 | 60 per vehicle over 18 months, strictly increasing |
| Drivers | 60 | Three of them can sign in - see the Keycloak table above |
| Certificates | 177 | Licences, medicals and ADR, some superseded by renewals |
| Bookings | 3,002 | Across 18 months: ~2,500 completed, ~250 confirmed, ~250 cancelled |
| Work orders | 400 | Every status; 200 already carry a supplier order id |
| Part order lines | 796 | On 180 of the 250 vehicles - the other 70 have never been in the workshop |
| Notifications | 0 | Deliberately. They appear when the expiry scanner runs - see below |
| Report jobs | 0 | Deliberately. One exists because somebody asked for it |

Among the drivers, four hold no licence at all, five hold one that has expired, and six hold one
expiring within the next 30 days. Those fifteen are the reason the eligibility rule is worth
testing, and the last six are what a `CertificateExpiringSoon` scan is supposed to find.

Two bookings are in there specifically to catch a wrong answer:

- one vehicle has **two bookings that touch but do not overlap** - the second starts at the exact
  instant the first ends. A closed-interval overlap check calls this a conflict. It is not one,
  and it is the mistake almost everybody makes first.
- another has a **cancelled booking sitting on top of a confirmed one**. That row can only exist
  because the exclusion constraint is partial, which is what makes cancelling actually free the
  slot.

Ids come from `DeterministicGuid`, which hashes a name into a stable GUID, so the Bookings seeder
can refer to `vehicle:42` without reading the `vehicles` schema. Dates are the one thing measured
relative to *today* rather than fixed: a licence that expired last year would stop being an
interesting test case the moment the calendar moved past it.

## The reference slice

`Fleet.Api` implements two resources in full. They exist to be read before you write your own, and
between them they cover every technique the rest of the course asks for.

| | |
|---|---|
| `GET /vehicles` | paging, filtering, an allow-listed sort, 30-second output cache |
| `GET/POST /vehicles/{id}/odometer-readings` | a sub-resource, and a domain rule surfacing as 400 |
| `PUT /vehicles/{id}/status` | PUT to a sub-resource rather than PATCH on the parent |
| `POST /vehicles` | 201 + `Location`, role-restricted to admins |
| `GET /bookings` | a driver's list is scoped by pinning the filter, not by discarding rows |
| `GET /bookings/{id}` | returns an `ETag`; resource-based authorization |
| `POST /bookings` | `Idempotency-Key`, 201 + `Location` + `ETag` |
| `PUT /bookings/{id}/schedule` | `If-Match` required: 428 without, 412 if stale |
| `DELETE /bookings/{id}` | idempotent 204, even though the domain calls a second cancel a conflict |

Four things are worth reading closely.

**Where the status code is decided.** `Http/ProblemResults.cs` maps `ErrorKind` to a status code
and an RFC 9457 document. That file is the entire answer to "who decides this is a 409?" - the
modules never do. And the mapping is not one-to-one: `BookingEndpoints` turns the same
`ErrorKind.Conflict` into a **412** when it came from a stale `If-Match`, because what failed is
the precondition the client attached rather than the request itself.

**Why `DELETE` answers 204 twice.** `IBookingService.CancelAsync` calls a second cancellation a
conflict, and it is right to: the domain models a state machine and that transition does not
exist. The endpoint disagrees, because `DELETE` is meant to be idempotent and the caller got what
they asked for. Both are correct at their own layer. That disagreement is the clearest example in
the repository of why `ErrorKind` is not a status code.

**How a driver is kept to their own bookings.** Two different mechanisms, because they are two
different problems. A single booking goes through `BookingAccessHandler`, an
`IAuthorizationHandler` that needs the booking itself - a policy on the endpoint cannot express
"but not that one". The *list* pins the `driverId` filter before the query runs; filtering
afterwards would still leak the true total through the pagination metadata.

**What the output cache is safe for.** `GET /vehicles` is cached for 30 seconds including for
signed-in callers, which the framework's default policy refuses to do. That is safe here only
because the vehicle list is identical for everyone. `GET /bookings` is not cached at all. Read
`VehicleListCachePolicy` before copying it.

## Two things worth reading before you write an endpoint

**The overlap rule is enforced twice.** `BookingService` checks for a clash before it inserts, and
Postgres refuses the row if one slips through:

```sql
EXCLUDE USING gist (
    vehicle_id WITH =,
    tstzrange(starts_at, ends_at, '[)') WITH &&
) WHERE (status <> 2)
```

The service check exists to produce a decent error message. It cannot be correct on its own -
between its `SELECT` and its `INSERT`, another transaction can commit a conflicting booking, and
under load it will. The constraint is what makes the rule true. `DoubleBookingTests` fires two
genuinely parallel bookings at the same window and asserts exactly one success and one conflict;
run it and watch.

Note `'[)'` - the range is half-open, matching `BookingWindow.Overlaps` in C# exactly. If those two
ever disagree, the database starts rejecting rows the service was happy with.

**`RowVersion` is Postgres' `xmin`.** Every row already carries the id of the transaction that last
wrote it, so optimistic concurrency costs no extra column and nothing for the application to
remember to increment. The DTO exposes it base64-encoded, which is what an `ETag` is built from:

```
GET  /bookings/{id}     ->  ETag: "AAAC7A=="
PUT  /bookings/{id}     <-  If-Match: "AAAC7A=="
```

Pass it back as `ExpectedRowVersion` and a stale value returns `Conflict` instead of silently
overwriting whoever got there first. The service accepts quoted and weak (`W/"..."`) forms, so an
`If-Match` header can go through unmodified. Whether your endpoint *requires* `If-Match` or merely
honours it is your call - and worth arguing about before you decide.

## The one call that leaves the process

`IMaintenanceService.OrderPartsAsync` sends part lines to the supplier through a typed
`HttpClient`. **That client has no timeout, no retry and no circuit breaker, on purpose.** It is
registered in `MaintenanceModuleExtensions` next to a `TODO(week-12)` explaining exactly what is
missing and what to think about before adding it.

Measure the damage before you fix it. A supplier that stalls for eight seconds holds your request
thread for eight seconds; `HttpClient`'s default timeout is a hundred. Everything the client does
do properly is the part that is not a policy decision: an upstream `500`, `429`, `503`, timeout or
refused connection all come back as `ErrorKind.Unavailable` rather than as an exception, because
an upstream having a bad day is an expected outcome of calling it. A `400` maps to
`ErrorKind.Validation` instead - the supplier understood us and disagreed, and retrying that
changes nothing.

Maintenance is the only module that returns `Unavailable`, which makes it the place to think hard
about the difference between "you cannot do that" (409) and "try again shortly" (503) - and about
which of the two deserves a `Retry-After` header.

## Watching an event travel between modules

Nothing seeds the `notifications` table. Its rows appear because Drivers announced something, which
is the one interesting thing about that module, and seeding it by hand would hide it. Start the API
and wait a few seconds:

```bash
dotnet run --project src/Fleet.Api
docker compose exec postgres psql -U fleet -d fleet -c "SELECT message FROM notifications.notifications LIMIT 5;"
```

What happened in between:

1. `CertificateExpiryScanner` (a hosted service in Drivers) finds certificates expiring within 30
   days, marks each one as warned, and enqueues a `CertificateExpiringSoon` into the **`drivers.outbox`**
   table. Both writes go in one `SaveChangesAsync`.
2. `OutboxProcessor` (in `Fleet.Common`) sweeps every module's outbox every five seconds and
   publishes what it finds through `IEventBus`.
3. `CertificateExpiringSoonHandler` in Notifications writes a row and logs a line.

Drivers has never heard of Notifications. Delete the Notifications module and Drivers carries on
unchanged, still announcing to nobody. That is the point of the arrangement, and the reason the
dependency arrow runs from the subscriber to the publisher's `.Contracts` and never the other way.

The outbox is what makes step 1 safe. Publishing straight to the bus would be simpler and wrong: a
crash between the commit and the publish loses the event with no trace. Delivery is **at least
once**, so the handler checks before it inserts - and `docker compose restart` mid-sweep is a
perfectly good way to see why it has to.

## Reports, and why 202 exists

Generating a utilisation CSV takes about twenty seconds. It does not have to - the arithmetic over
250 vehicles takes milliseconds - but `Reporting:SimulatedDuration` makes it, because a report that
returned instantly would make the whole asynchronous-job pattern look like ceremony.

```
POST /reports        -> 202 Accepted, Location: /reports/{id}    (job is Queued)
GET  /reports/{id}   -> 200, status Queued | Running | Completed | Failed
GET  /reports/{id}/content -> the CSV, once it exists
```

`IReportService.EnqueueAsync` returns in milliseconds with a job id, and `ReportJobProcessor` does
the work in the background and puts the CSV in Azurite. Asking for the file before it is ready
returns `Conflict`, not `NotFound` - the job is real, it simply has nothing to give yet, and telling
those two apart is how a client knows whether to keep polling.

## Idempotency

`IIdempotencyStore` is finished; the middleware that uses it is written once in `Fleet.Api` as the
worked example. The store's one job is to be atomic, and it is, because the client's key is the
primary key of `shared.idempotency_keys` and claiming it is a single
`INSERT ... ON CONFLICT DO NOTHING`. `IdempotencyStoreTests` fires twenty simultaneous claims at
the same key and asserts that exactly one wins.

Note `AbandonAsync`. Without it, one 500 would poison a key forever and the client's perfectly
reasonable retry would be refused until the purge job came round.

## Working with migrations

Each module owns its migrations and its own `__ef_migrations_history` table, inside its own schema.
Each has a design-time factory, so no startup project is involved:

```bash
dotnet ef migrations add <Name> --project src/Modules/Vehicles/Fleet.Modules.Vehicles
dotnet ef migrations has-pending-model-changes --project src/Modules/Drivers/Fleet.Modules.Drivers
```

Migrations are generated code and exempt from the style rules in `.editorconfig`. Do not hand-edit
them to satisfy a formatter.

## Repository layout

```
src/
  Fleet.Api/                  reference host - Vehicles + Bookings, implemented
  Fleet.Api.Workshop/         your host - DI wired, zero endpoints
  Fleet.Common/               shared kernel: Result<T>, paging, clock, events, outbox, blobs
  Modules/<Name>/
    Fleet.Modules.<Name>/           implementation, internal types, owns one Postgres schema
    Fleet.Modules.<Name>.Contracts/ public interfaces and DTOs, referenced by other modules
  Supplier.Api.Fake/          the unreliable upstream
tests/
  Fleet.Architecture.Tests/   module boundary rules, enforced
  Fleet.Modules.*.Tests/      unit tests per module
  Fleet.Api.IntegrationTests/ reference slice only
docs/adr/                     one ADR per design decision
docs/assignments/             one specification per week
requests/                     .http files for the reference slice
```

## The rules the architecture tests enforce

`tests/Fleet.Architecture.Tests` fails the build when any of these is broken. They are not
suggestions, and the fix is almost never to change the test:

1. **No module references ASP.NET Core.** Not the package, not the framework reference, not a
   single type. Application services return `Result<T>` with an `ErrorKind`; choosing the status
   code is the API layer's job, which for most of this course means yours.
2. **A module may reference other modules' `.Contracts` projects, never their implementations.**
   Bookings stores a `VehicleId` as a plain `Guid` and asks `IVehicleCatalog` whether it exists.
3. **Nothing internal leaks through a public contract.** A public interface returning an internal
   type compiles inside its own assembly and is useless from anywhere else.
4. **`Fleet.Common` references nothing.** The shared kernel is the bottom of the graph.

Each module owns exactly one Postgres schema and there are **no cross-schema foreign keys**. That
is a deliberate constraint, and the reason cross-module state changes travel through an outbox
rather than a shared transaction.

## Make targets

| Target | Does |
|---|---|
| `make up` | Start the infrastructure, wait for health, print the port table |
| `make down` | Stop it, keeping the data |
| `make reset` | Destroy every volume, start clean, migrate and reseed |
| `make test` | Run every test |
| `make api` / `make workshop` | Run either host |
| `make logs` | Tail the infrastructure logs |

### Without make (PowerShell or plain shell)

```powershell
docker compose up -d --wait                          # make up
docker compose down                                  # make down
docker compose down -v; docker compose up -d --wait  # make reset
dotnet test Fleet.sln                                # make test
dotnet run --project src/Fleet.Api                   # make api
dotnet run --project src/Fleet.Api.Workshop          # make workshop
```

## Where the documentation is

| | |
|---|---|
| [`docs/architecture.md`](docs/architecture.md) | The module dependency diagram, and how a request and an event travel |
| [`docs/adr/`](docs/adr/) | Six decisions, each with what it cost |
| [`docs/assignments/`](docs/assignments/) | Fourteen weeks. Titles are a proposal; the specifications are the course author's |
| [`requests/`](requests/) | `.http` files for the reference slice |

The ADRs are worth reading before week 1. Several explain why something you are about to be asked
to build is shaped the way it is, and [ADR 4](docs/adr/0004-result-instead-of-exceptions.md)
explains why the most important decision in the course — which status code — is left to you.

## Notes for the curious

- **Tests run on Microsoft.Testing.Platform**, not VSTest. That is what the `test` section in
  `global.json` selects, and xunit.v3 on the .NET 10 SDK requires it. It is also why the test
  projects reference only `xunit.v3` and are built as executables.
- **Package versions live in `Directory.Packages.props`.** Putting a `Version` on a
  `PackageReference` anywhere else is a build error.
- **Warnings are errors**, everywhere, via `Directory.Build.props`. A warning you are allowed to
  ignore is a warning nobody reads.
