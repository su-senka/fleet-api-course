# 3. No MediatR, no CQRS framework, no generic repository

Status: accepted

## Context

A .NET codebase of this shape usually acquires three things without anybody deciding to: MediatR,
a `Command`/`Query` handler per operation, and an `IRepository<T>` over EF Core. Each is defensible
in a large system with many contributors.

Each is also a layer of indirection between a student and the thing they are trying to learn. This
repository has exactly one job: teach six competent .NET developers how to build an HTTP API. Every
hop between the endpoint and the SQL is a hop they have to understand before they can see the part
that matters.

## Decision

Module contracts are plain interfaces. Implementations are plain classes that call EF Core
directly. `IVehicleService.ListAsync` is a method on a class; the class runs a LINQ query on a
`DbContext`.

No mediator, no pipeline behaviours, no handler-per-operation, no repository abstraction over EF
Core.

## Consequences

**What it buys.** "Go to definition" on `IVehicleService.ListAsync` lands on the code that runs.
There is no registration convention to learn, no assembly scanning, and no way for a pipeline
behaviour to change what a call does without appearing in the call. A stack trace has four frames
in it rather than fourteen.

It also puts EF Core in plain view, which is the point. Students see `AsNoTracking`, see a
projection avoid loading entities, and see where an N+1 would appear. A generic repository hides
exactly those things - and hides them behind an abstraction that cannot express half of what EF
Core does anyway.

**What it costs.** Cross-cutting concerns have nowhere central to live. There is no pipeline
behaviour to add logging or validation to every command, so validation happens at the HTTP edge
(FluentValidation, in `Fleet.Api`) and inside entities, and neither place covers the other. In a
larger system that duplication would start to hurt.

Application services are also somewhat long. `BookingService` is about 300 lines and does six
things. Split into handlers it would be six files of fifty lines each - genuinely tidier, and
genuinely harder to read end to end for the first time.

**Reversing it is cheap, and that is deliberate.** Every service is behind an interface in a
`.Contracts` project. Introducing MediatR later means changing the implementations and the
registration, and no caller. If the course grows to want it, it is one module's worth of work -
which is why it is not here now.
