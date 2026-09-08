using System.Text.Json;

namespace Fleet.Modules.Reporting.Application;

/// <summary>
/// The JSON settings used for a report job's stored parameters.
/// </summary>
/// <remarks>
/// Shared between the service that writes them and the processor that reads them back. Two
/// different sets of options would work perfectly until somebody changed one of them.
/// </remarks>
internal static class ReportServiceJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
