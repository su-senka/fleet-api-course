# 1. A modular monolith, not a layered solution

Status: accepted

## Context

The usual shape for a .NET solution of this size is horizontal: `Fleet.Domain`, `Fleet.Application`,
`Fleet.Infrastructure`, `Fleet.Api`. Everybody has seen it and it needs no explanation.

It also has a property that is fatal for teaching. In a layered solution the only enforced boundary
runs *between the layers*, so `Fleet.Application` ends up holding the application services for
vehicles, drivers, bookings and everything else, with nothing at all stopping the booking service
from calling a vehicle repository directly. The seams that matter in a real system - the ones
between features - are the seams the compiler cannot see.

This repository needs students to feel a boundary. Six weeks in, somebody will want a query that
joins bookings to vehicles, and what happens next is the lesson.

## Decision

Slice vertically. Each module - Vehicles, Drivers, Bookings, Maintenance, Reporting, Notifications
- is a pair of projects: an implementation, and a `.Contracts` project holding the interfaces and
DTOs other modules may use.

A module may reference `Fleet.Common` and other modules' `.Contracts` projects. Never another
module's implementation. `Fleet.Architecture.Tests` fails the build when that is broken.

There is still layering, but it is *inside* each module: `Domain/`, `Application/`, `Persistence/`.
The layers are a detail of the module rather than the structure of the solution.

## Consequences

**What it buys.** The boundary is enforced by the compiler, not by discipline. When somebody wants
that join, they cannot write it: `Fleet.Modules.Bookings` cannot see `Vehicle`. They have to ask
`IVehicleCatalog` instead, and the cost of the coupling becomes visible at the moment they create
it rather than two years later.

**What it costs.** Twelve projects for six modules, and a build that takes longer than it needs to.
Adding a module means creating two projects and wiring them up, which is friction a layered
solution does not have.

**The real cost is queries.** Utilisation reporting genuinely needs bookings and vehicles together,
and there is no SQL join available. `Reporting` reads both through their contracts and joins them
in memory - a few milliseconds for 250 vehicles, and entirely the wrong design at fifty thousand.
That trade is stated at the site, in `UtilisationReportGenerator`, and it is honest: a real system
at that scale would need a read model fed by events.

**What we are not claiming.** This is not preparation for microservices, and the modules are not
"services waiting to be extracted". They share a process, a database and a deployment, and every
in-process call here would become a network call with its own failure modes. The boundary is worth
having on its own terms.
