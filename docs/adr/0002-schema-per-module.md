# 2. One Postgres schema per module, and no cross-schema foreign keys

Status: accepted

## Context

Project boundaries stop at the compiler. If all six modules share one schema and one `DbContext`,
the database quietly puts back every coupling the projects removed: one migration history, one
model, and a foreign key from `bookings` to `vehicles` that means the two can never be separated,
deployed independently, or reasoned about apart.

A boundary the database does not respect is not a boundary.

## Decision

Each module owns one schema - `vehicles`, `drivers`, `bookings`, `maintenance`, `reporting`,
`notifications` - and its own `DbContext`, with its own `__ef_migrations_history` table inside its
own schema.

No foreign keys cross a schema. `Bookings` stores `VehicleId` as a plain `Guid` and asks
`IVehicleCatalog` whether it means anything.

A seventh schema, `shared`, holds the idempotency key store. It belongs to no module because
idempotency is a property of an HTTP request rather than of vehicles or bookings.

## Consequences

**What it buys.** Each module migrates independently: `dotnet ef migrations add` against Bookings
cannot touch a table Vehicles owns. A curious student can run `\dn` in psql and see the
architecture. And the absence of cross-schema foreign keys makes the in-memory join in Reporting
unavoidable rather than merely discouraged, which is the point.

**What it costs, and it is a real cost.** Referential integrity is gone. Nothing in the database
stops a booking pointing at a vehicle that has been deleted, and `BookingService` handles that by
rendering `(unknown)` rather than failing the whole page. That is a deliberate choice - a cosmetic
gap in one row is better than a failed request - but it is a guarantee we have given up, and
somebody will eventually be surprised by it.

**Seeding has to be ordered by hand.** With no foreign keys there is nothing to enforce that
vehicles exist before bookings refer to them. `IModuleDatabaseInitializer.Order` does it explicitly,
and the seeders agree on ids through `DeterministicGuid` rather than by reading each other's tables.

**Cross-module transactions do not exist.** A request that changes two modules commits twice, and
can fail in between. That is the reason the outbox exists - see ADR 6.
