# 6. Cross-module state changes travel through a transactional outbox

Status: accepted

## Context

The Drivers module notices a certificate expiring within thirty days. Notifications needs to record
it. There is no shared transaction between the two - each module has its own `DbContext` and its own
schema (ADR 2) - so "mark the certificate warned, then tell Notifications" is two operations that
can fail independently.

Publishing straight to an in-process event bus after `SaveChanges` looks like it works and does not:

- crash between the commit and the publish, and the event is gone with no trace that it existed;
- let the handler throw, and either the publisher's committed work is inconsistent with a
  subscriber that never ran, or - worse - the publisher rolls back work it already committed.

Neither failure leaves anything behind to diagnose.

## Decision

A publishing module writes the event to an `outbox` table **in its own schema**, in the same
`SaveChangesAsync` as the state change that caused it. A background service (`OutboxProcessor` in
`Fleet.Common`) sweeps every module's outbox and publishes what it finds through `IEventBus`.

Publish first, mark processed second. A handler that throws leaves the row pending, with an attempt
count and the last error on it.

## Consequences

**What it buys.** The event and the change it describes are atomic: either the certificate is
marked warned and the `CertificateExpiringSoon` row exists, or neither does. A message that cannot
be delivered stays in a table with a readable reason on it, which is worth a great deal at 2am.

The coupling stays one-directional. Notifications references `Fleet.Modules.Drivers.Contracts` for
the event type; Drivers has never heard of Notifications and would work unchanged if it were
deleted.

**Delivery is at least once, and that is not a detail.** A process that dies between publishing and
marking the row processed will publish again on restart. Every handler must tolerate seeing the same
event twice - `CertificateExpiringSoonHandler` checks before it inserts, behind a unique index. A
handler that assumes exactly-once is a bug waiting for a deployment.

**It is eventually consistent.** Up to five seconds pass between a certificate being marked and the
notification appearing. For this it does not matter. For anything a user is watching, it would.

**One process only.** Nothing locks the row, so two instances would happily dispatch the same
message twice. A real deployment needs `FOR UPDATE SKIP LOCKED` or leader election. That is stated
in `OutboxProcessor` rather than pretended away, and it is deliberately out of scope: this
repository runs on a laptop.

**The bus is in-process, and that is not a message broker.** An event published here reaches its
handler a microsecond later, in the same process, and a failing handler fails the dispatch. Swapping
in a real broker would be one class plus a great deal of thinking about ordering and delivery. The
shape is right; the guarantees are not the same.
