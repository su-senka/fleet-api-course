-- Runs once, on first start of an empty postgres volume.
--
-- Each module owns exactly one schema and never writes outside it. Creating them here rather
-- than in a migration means the very first `dotnet ef database update` has somewhere to land,
-- and it keeps the schema list in one readable place.
--
-- Extensions are created here for the same reason: btree_gist is what makes the Bookings
-- exclusion constraint possible (EXCLUDE USING gist needs it to mix a uuid equality with a
-- range overlap), and it must exist before the migration that uses it runs.

CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE SCHEMA IF NOT EXISTS vehicles;
CREATE SCHEMA IF NOT EXISTS drivers;
CREATE SCHEMA IF NOT EXISTS bookings;
CREATE SCHEMA IF NOT EXISTS maintenance;
CREATE SCHEMA IF NOT EXISTS reporting;
CREATE SCHEMA IF NOT EXISTS notifications;

-- Not a module. Holds the idempotency key store, which belongs to the HTTP layer rather than to
-- vehicles or bookings, and would look owned by whichever module it was parked in.
CREATE SCHEMA IF NOT EXISTS shared;

GRANT ALL ON SCHEMA vehicles, drivers, bookings, maintenance, reporting, notifications, shared TO fleet;
