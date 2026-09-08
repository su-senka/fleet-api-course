using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Fleet.Api.OpenApi;

/// <summary>
/// Describes the API, and gives the request bodies realistic examples.
/// </summary>
/// <remarks>
/// A generated schema tells a reader what fields exist; an example tells them what a good request
/// looks like. The difference between <c>"startsAt": "string"</c> and a real ISO-8601 instant is
/// the difference between a client developer guessing and knowing.
/// </remarks>
internal sealed class FleetDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Fleet API (reference)",
            Version = "v1",
            Description =
                "The worked example for the Fleet workshop. Vehicles and Bookings are implemented "
                + "in full; every other module is finished but has no HTTP surface, and putting "
                + "one on it is the course.\n\n"
                + "Errors are RFC 9457 problem documents. Branch on the `code` extension rather "
                + "than on the human-readable `detail`.\n\n"
                + "Get a token from Keycloak at http://localhost:8080/realms/fleet - the README "
                + "has the curl command and the six usernames.",
        };

        document.Servers = [new OpenApiServer { Url = "http://localhost:5100", Description = "Local" }];

        return Task.CompletedTask;
    }
}

/// <summary>Adds examples to the request bodies that benefit most from one.</summary>
internal sealed class FleetExampleTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // For a minimal API the name set by .WithName() lives in the endpoint metadata.
        // AttributeRouteInfo.Name is an MVC concept and is null here, which is a quiet way to
        // produce a transformer that runs and does nothing.
        var endpointName = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IEndpointNameMetadata>()
            .FirstOrDefault()
            ?.EndpointName;

        var example = ExampleFor(endpointName);

        if (example is not null && operation.RequestBody?.Content is { } content)
        {
            foreach (var mediaType in content.Values)
            {
                mediaType.Example = example;
            }
        }

        AddCommonResponseHeaders(operation, endpointName);

        return Task.CompletedTask;
    }

    private static System.Text.Json.Nodes.JsonNode? ExampleFor(string? endpointName) => endpointName switch
    {
        "RegisterVehicle" => Example("""
            {
              "plate": "4AB 7712",
              "type": "Van",
              "depotId": "00000000-0000-0000-0000-000000000000",
              "odometerKm": 12500
            }
            """),

        "ChangeVehicleStatus" => Example("""{ "status": "InMaintenance" }"""),

        "RecordOdometerReading" => Example("""
            {
              "recordedAt": "2026-06-01T09:30:00Z",
              "km": 128450
            }
            """),

        "CreateBooking" => Example("""
            {
              "vehicleId": "00000000-0000-0000-0000-000000000000",
              "driverId": "00000000-0000-0000-0000-000000000000",
              "purpose": "Rozvoz Praha - Brno",
              "startsAt": "2026-07-01T08:00:00Z",
              "endsAt": "2026-07-01T17:00:00Z"
            }
            """),

        "RescheduleBooking" => Example("""
            {
              "startsAt": "2026-07-02T08:00:00Z",
              "endsAt": "2026-07-02T17:00:00Z"
            }
            """),

        _ => null,
    };

    /// <summary>
    /// Documents the headers that are part of the contract but invisible to the schema generator.
    /// </summary>
    /// <remarks>
    /// A client cannot use <c>If-Match</c> if nothing tells them the ETag exists. These are the
    /// details that make the difference between a spec that compiles and one somebody can work from.
    /// </remarks>
    private static void AddCommonResponseHeaders(OpenApiOperation operation, string? endpointName)
    {
        if (endpointName is not ("GetBooking" or "CreateBooking" or "RescheduleBooking"))
        {
            return;
        }

        foreach (var response in operation.Responses ?? [])
        {
            if (!response.Key.StartsWith('2') || response.Value is not OpenApiResponse concrete)
            {
                continue;
            }

            concrete.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);

            concrete.Headers["ETag"] = new OpenApiHeader
            {
                Description = "This revision of the booking. Send it back in If-Match to update it.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            };
        }
    }

    private static System.Text.Json.Nodes.JsonNode Example(string json) =>
        System.Text.Json.Nodes.JsonNode.Parse(json)!;
}
