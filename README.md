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
dotnet run --project src/Fleet.Api             # reference API on :5100
dotnet run --project src/Fleet.Api.Workshop    # your API on :5101
```

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
