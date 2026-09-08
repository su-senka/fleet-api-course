# 4. Application services return `Result<T>`, not exceptions

Status: accepted

## Context

The alternative is familiar: throw `NotFoundException`, `ConflictException`, `ValidationException`,
and map them to status codes in an exception handler. It works, it is common, and it is less code.

It has two problems for this repository specifically.

The first is that it makes the HTTP mapping invisible. An exception filter that turns
`NotFoundException` into 404 is a lookup table somebody wrote once, and a student never has to
think about it again. But choosing status codes *is the course*. If the mapping happens behind the
scenes, the most important decision in the repository is the one nobody sees.

The second is that "this vehicle is already booked" is not exceptional. It is a normal, expected
outcome of asking to book a vehicle - one of the two things that can happen. Using the exception
mechanism for it means the ordinary path of a booking request goes through stack unwinding.

## Decision

Application services return `Result` or `Result<T>`. A failure carries an `Error` with an
`ErrorKind` - `NotFound`, `Conflict`, `Validation`, `Forbidden`, `Unavailable` - a stable
machine-readable code, and a human-readable message.

`ErrorKind` is deliberately HTTP-agnostic. There is no `BadRequest` and no `PreconditionFailed`.

Exceptions stay for genuine faults: a dropped connection, a bug, a violated invariant.

## Consequences

**What it buys.** The mapping is a file students can read and argue with:
`Fleet.Api/Http/ProblemResults.cs`. And because the kind is not a status code, the same failure can
mean different things in different places - `ErrorKind.Conflict` is a 409 for an overlapping
booking and a **412** when it came from a stale `If-Match`, because there what failed is the
precondition the client attached. That distinction is impossible to express if the module has
already decided the answer.

It also makes failure paths visible in the signature. `Task<Result<BookingDto>>` says this can
fail; `Task<BookingDto>` says nothing.

**What it costs.** Verbosity, and there is no getting around it. Every call site checks
`IsFailure` and returns early. A method that makes four fallible calls has four of these blocks,
and C# has no `?` operator to collapse them the way some languages do.

`Result` is a class, not a struct, so every service call allocates. Irrelevant here; worth knowing
before copying the pattern into a hot path.

And the discipline is not enforced. Nothing stops a service throwing, and `ToDtosAsync` in
`BookingService` does throw if a booking arrives without its depot loaded - because that is a
programming error, not an outcome. Deciding which is which requires judgement every time.
