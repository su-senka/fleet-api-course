using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fleet.Common.Messaging.Outbox;

/// <summary>
/// Turns integration events into stored JSON and back.
/// </summary>
/// <remarks>
/// The stored <see cref="OutboxMessage.Type"/> is the event's assembly-qualified name, minus the
/// version and culture noise. That is enough to find the type again without being so specific that
/// a patch release breaks every pending message in the table.
/// </remarks>
internal static class OutboxSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string TypeNameOf(IntegrationEvent integrationEvent) =>
        TypeNameOf(integrationEvent.GetType());

    public static string TypeNameOf(Type type) =>
        $"{type.FullName}, {type.Assembly.GetName().Name}";

    public static string Serialize(IntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), Options);

    /// <summary>
    /// Reads a stored message back, or returns <c>null</c> when its type no longer exists.
    /// </summary>
    /// <remarks>
    /// A missing type means somebody deleted or renamed an event class while messages were still
    /// pending. The dispatcher records that as a failure on the row rather than crashing, so the
    /// rest of the batch still goes out and the offending row can be looked at by hand.
    /// </remarks>
    public static IntegrationEvent? Deserialize(OutboxMessage message)
    {
        var type = Type.GetType(message.Type, throwOnError: false);

        if (type is null || !typeof(IntegrationEvent).IsAssignableFrom(type))
        {
            return null;
        }

        return JsonSerializer.Deserialize(message.Payload, type, Options) as IntegrationEvent;
    }
}
