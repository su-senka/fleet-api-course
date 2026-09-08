# Endpoints

One stub per resource. Every file here compiles, is not called from `Program.cs`, and carries a
`TODO(week-N)` pointing at the assignment that fills it in.

| Stub | Weeks | Module contract |
|---|---|---|
| `DepotEndpoints` | 1 | `IDepotService` |
| `VehicleEndpoints` | 2-4 | `IVehicleService` |
| `DriverEndpoints` | 4-5 | `IDriverService` |
| `CertificateEndpoints` | 6 | `IDriverService`, `IBlobStore` |
| `BookingEndpoints` | 7-10 | `IBookingService`, `IDriverDirectory` |
| `WorkOrderEndpoints` | 11-12 | `IMaintenanceService` |
| `ReportEndpoints` | 13 | `IReportService` |
| `NotificationEndpoints` | 14 | `INotificationService` |

The finished implementations of `VehicleEndpoints` and `BookingEndpoints` live in
`src/Fleet.Api/Endpoints/`, along with everything they lean on: `Http/ProblemResults.cs` for the
RFC 9457 mapping, `Http/PagingParameters.cs` for the query-string conventions,
`Auth/BookingAuthorization.cs` for the resource-based rule, and
`Middleware/IdempotencyMiddleware.cs`.

Those two are the only resources in this repository that are finished. Everything else is yours.
Write it first and compare afterwards - reading the answer before attempting the question is a
reliable way to feel like you understood something you did not.
