# Architecture decision records

One short record per design decision, in the format: context, decision, consequences.

Planned, and written in milestone 7:

- modular monolith over a layered solution
- schema per module
- no MediatR
- `Result<T>` instead of exceptions
- `xmin`-based optimistic concurrency
- outbox for cross-module events

The point of an ADR is the *consequences* section. A decision without a stated cost is a
preference wearing a suit.
