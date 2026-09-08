# Architecture

A modular monolith: one process, one database, six modules that cannot see into each other.

The decisions behind this, and what each of them costs, are in [`adr/`](adr/). This page is the
map.

## Module dependencies

Both hosts reference all six module implementations and nothing else - that part is uniform and
uninteresting. What matters is the middle of this diagram: every arrow is a project reference the
compiler enforces, and `tests/Fleet.Architecture.Tests` fails the build if one appears that should
not.

```mermaid
graph TD
    subgraph Impl["Module implementations (internal types)"]
        V["Vehicles"]
        D["Drivers"]
        B["Bookings"]
        M["Maintenance"]
        R["Reporting"]
        N["Notifications"]
    end

    subgraph Contracts["Module contracts (public interfaces and DTOs)"]
        VC["Vehicles.Contracts"]
        DC["Drivers.Contracts"]
        BC["Bookings.Contracts"]
        MC["Maintenance.Contracts"]
        RC["Reporting.Contracts"]
        NC["Notifications.Contracts"]
    end

    K["Fleet.Common<br/><i>shared kernel</i>"]

    V --> VC
    D --> DC
    B --> BC
    M --> MC
    R --> RC
    N --> NC

    B -->|"exists? bookable?"| VC
    B -->|"exists? may drive?"| DC
    M -->|"exists?"| VC
    R -->|"the whole fleet"| VC
    R -->|"booked periods"| BC
    N -->|"the event type"| DC

    VC & DC & BC & MC & RC & NC --> K

    classDef kernel fill:#e8e8e8,stroke:#666,stroke-width:2px
    classDef contract fill:#dcfce7,stroke:#22c55e
    class K kernel
    class VC,DC,BC,MC,RC,NC contract
```

Read the middle of that diagram carefully. **Bookings depends on `Vehicles.Contracts`, never on
`Vehicles`.** It cannot see the `Vehicle` entity, cannot write a join against the `vehicles` schema,
and cannot know that a vehicle has an odometer. It asks `IVehicleCatalog` whether a vehicle exists
and `IVehicleAvailability` whether it may be booked, and those answers are all it gets.

The one arrow that surprises people is `Notifications --> Drivers.Contracts`. The dependency runs
from the *subscriber* to the *publisher*, because Notifications needs the
`CertificateExpiringSoon` type to deserialise it. Drivers has never heard of Notifications and
would work unchanged if it were deleted.

## What each module owns

| Module | Schema | Owns | Cross-module reads |
|---|---|---|---|
| Vehicles | `vehicles` | Vehicles, depots, odometer readings | none |
| Drivers | `drivers` | Drivers, certificates, its own outbox | none |
| Bookings | `bookings` | Bookings | Vehicles, Drivers |
| Maintenance | `maintenance` | Work orders, part lines | Vehicles |
| Reporting | `reporting` | Report jobs | Vehicles, Bookings |
| Notifications | `notifications` | Notifications | Drivers (the event type only) |
| — | `shared` | Idempotency keys | belongs to no module |

No foreign key crosses a schema. That is enforced by nothing except review and the absence of a
navigation property to write one with, and it is checked in `psql` rather than in a test - see
ADR 2 for what it costs.

## How a request travels

`GET /bookings?status=Confirmed&page=2` through the reference host:

```mermaid
sequenceDiagram
    participant C as Client
    participant E as BookingEndpoints
    participant P as ProblemResults
    participant S as BookingService
    participant VC as IVehicleCatalog
    participant DB as Postgres

    C->>E: GET /bookings?status=Confirmed&page=2
    Note over E: reads PageRequest, SortRequest,<br/>FilterRequest from the query string
    Note over E: a driver's driverId is pinned<br/>into the filter here
    E->>S: ListAsync(page, sort, filter)
    S->>DB: SELECT COUNT(*) ... WHERE status = 1
    S->>DB: SELECT ... LIMIT 20 OFFSET 20
    S->>VC: GetManyAsync(20 vehicle ids)
    VC->>DB: SELECT ... WHERE id = ANY($1)
    Note over S: one call, not twenty:<br/>this is where the N+1 would be
    S-->>E: Result of PagedResult of BookingDto
    alt success
        E-->>C: 200 + JSON
    else failure
        E->>P: From(error, httpContext)
        Note over P: ErrorKind → status code.<br/>The one place that decision is made.
        P-->>C: 4xx + application/problem+json
    end
```

The only thing the endpoint decides is HTTP. Whether the booking may be made, whether the window
clashes, whether the driver holds a licence - all of that happened inside the module, and all of it
arrived as a `Result`.

## How an event travels

Nothing calls the Notifications module. Its rows appear because Drivers announced something:

```mermaid
sequenceDiagram
    participant Scan as CertificateExpiryScanner<br/>(Drivers)
    participant DDb as drivers schema
    participant Proc as OutboxProcessor<br/>(Fleet.Common)
    participant Bus as InProcessEventBus
    participant H as CertificateExpiringSoonHandler<br/>(Notifications)
    participant NDb as notifications schema

    Note over Scan: hourly
    Scan->>DDb: find certificates expiring in 30 days
    Scan->>DDb: mark warned + INSERT outbox row
    Note over Scan,DDb: one SaveChangesAsync:<br/>both writes, or neither
    Note over Proc: every 5 seconds
    Proc->>DDb: SELECT ... WHERE processed_at IS NULL
    Proc->>Bus: PublishAsync(CertificateExpiringSoon)
    Bus->>H: HandleAsync(event)
    H->>NDb: INSERT notification (if not already there)
    H-->>Bus: ok
    Bus-->>Proc: ok
    Proc->>DDb: UPDATE outbox SET processed_at
    Note over Proc,DDb: publish first, mark second:<br/>at least once, never at most once
```

The handler checks before it inserts because delivery is at-least-once and it *will* see a
duplicate eventually. See ADR 6.

## Where the interesting code is

If you read six files before starting, read these:

| File | Why |
|---|---|
| `Fleet.Common/Results/Result.cs` | The type every application service returns |
| `Fleet.Api/Http/ProblemResults.cs` | Where an `ErrorKind` becomes a status code |
| `Fleet.Api/Endpoints/BookingEndpoints.cs` | ETags, preconditions, idempotency, resource auth |
| `Modules/Bookings/.../BookingService.cs` | Two layers of defence against a double booking |
| `Modules/Bookings/.../Migrations/*_InitialBookings.cs` | The `EXCLUDE USING gist` constraint |
| `Modules/Maintenance/.../MaintenanceModuleExtensions.cs` | The `TODO(week-12)`, and why it is there |

## Infrastructure

```mermaid
graph LR
    API["Fleet.Api :5100"]
    WS["Fleet.Api.Workshop :5101"]
    PG[("postgres:5432<br/>7 schemas")]
    KC["keycloak:8080"]
    SEQ["seq:8081"]
    JG["jaeger:16686"]
    AZ["azurite:10000"]
    SUP["supplier-fake:5080<br/><i>fails on purpose</i>"]

    API --> PG & KC & SEQ & JG & AZ & SUP
    WS --> PG & SEQ & JG & AZ
    SUP --> SEQ

    classDef bad fill:#fee2e2,stroke:#ef4444
    class SUP bad
```

Both APIs run on your machine with `dotnet run`; everything else is in `docker compose`. The
supplier is the only component that is meant to fail, and it does so reproducibly - same
`FAKE_SEED`, same failures, every run.
