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
start — mostly Keycloak importing the realm. Then open <http://localhost:5101/scalar> and admire
the empty API surface.

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

Every decision it makes is logged, so you can line its log up against your circuit breaker's and
see exactly which request opened it.

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

## Notes for the curious

- **Tests run on Microsoft.Testing.Platform**, not VSTest. That is what the `test` section in
  `global.json` selects, and xunit.v3 on the .NET 10 SDK requires it. It is also why the test
  projects reference only `xunit.v3` and are built as executables.
- **Package versions live in `Directory.Packages.props`.** Putting a `Version` on a
  `PackageReference` anywhere else is a build error.
- **Warnings are errors**, everywhere, via `Directory.Build.props`. A warning you are allowed to
  ignore is a warning nobody reads.
