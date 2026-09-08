# Endpoints

One file per resource. Every file in this folder is a stub: it compiles, it is not called from
`Program.cs`, and it contains a `TODO(week-N)` pointing at the assignment that fills it in.

The reference implementations of `VehicleEndpoints` and `BookingEndpoints` live in
`src/Fleet.Api/Endpoints/`, along with everything they lean on: `Http/ProblemResults.cs` for the
RFC 9457 mapping, `Http/PagingParameters.cs` for the query-string conventions,
`Auth/BookingAuthorization.cs` for the resource-based rule, and
`Middleware/IdempotencyMiddleware.cs`. Read those before writing your own - they are the only two
resources in this repository that are finished, and they exist to be copied from.

Stub files arrive in milestone 7.
