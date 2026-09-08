# Architecture decision records

One short record per decision, in the format: context, decision, consequences.

| | Decision |
|---|---|
| [1](0001-modular-monolith-over-layered.md) | A modular monolith, not a layered solution |
| [2](0002-schema-per-module.md) | One Postgres schema per module, and no cross-schema foreign keys |
| [3](0003-no-mediatr.md) | No MediatR, no CQRS framework, no generic repository |
| [4](0004-result-instead-of-exceptions.md) | Application services return `Result<T>`, not exceptions |
| [5](0005-xmin-optimistic-concurrency.md) | Optimistic concurrency on bookings, using Postgres `xmin` |
| [6](0006-outbox-for-cross-module-events.md) | Cross-module state changes travel through a transactional outbox |

The point of an ADR is the **consequences** section. A decision without a stated cost is a
preference wearing a suit, and every one of these has a cost - an in-memory join, a lost
referential integrity guarantee, five seconds of inconsistency, a transaction id that wraps.

They are worth reading before the first assignment. Several of them explain why something you are
about to be asked to build is shaped the way it is, and one of them - ADR 4 - explains why the
most important decision in the course is left to you.
