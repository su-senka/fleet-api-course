# 5. Optimistic concurrency on bookings, using Postgres `xmin`

Status: accepted

## Context

Two dispatchers open the same booking. One moves it to Tuesday, the other to Wednesday. Without a
concurrency check the second write wins silently and the first dispatcher never learns their change
was discarded. Last-write-wins is not a decision anybody makes on purpose; it is what happens when
nobody decides.

The usual .NET answer is a `byte[] RowVersion` column with `[Timestamp]`, which is a SQL Server
feature. Postgres has no `rowversion`. The options are a `bigint` version column the application
increments, a trigger, or the `xmin` system column every Postgres row already has.

## Decision

Map `xmin` as the concurrency token on `Booking`:

```csharp
builder.Property(booking => booking.RowVersion)
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .ValueGeneratedOnAddOrUpdate()
    .IsConcurrencyToken();
```

Expose it on the DTO as a base64 string, so the API layer can build an `ETag` from it. The service
takes an optional expected version and returns `ErrorKind.Conflict` when it does not match.

## Consequences

**What it buys.** No extra column, no trigger, and nothing for application code to remember to
increment - `xmin` is the id of the transaction that last wrote the row, so Postgres maintains it
whether we ask or not. EF Core puts it in the `UPDATE`'s `WHERE` clause; a stale value affects zero
rows and raises `DbUpdateConcurrencyException`.

It maps onto HTTP almost too neatly. `xmin` identifies a revision, which is exactly what a strong
`ETag` is for, and `If-Match` is exactly the precondition it belongs in.

**What it costs.** `xmin` is a 32-bit transaction id and it *wraps* - after roughly four billion
transactions, and again after each `VACUUM FREEZE`. A token issued before a wraparound could in
principle match a row it does not describe. The window is astronomically unlikely and the
consequence is one lost update, but it is not zero, and a system where that matters wants a real
version column.

It is also Postgres-specific. Moving to another database means changing the mapping, though not
the API - which is part of why the token is opaque base64 rather than a number.

**A property of `xmin` worth knowing:** it changes on *any* update to the row, including one that
changes nothing a client cares about. A background job touching a booking invalidates every ETag
for it. That is correct behaviour for a strong validator and it will look like a bug the first time.

**Encoding it was deliberate.** A bare integer invites a client to send `version + 1` and see what
happens. Base64 of four big-endian bytes looks like what it is: a token to hand back unchanged.
