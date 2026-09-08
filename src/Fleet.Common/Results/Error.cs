namespace Fleet.Common.Results;

/// <summary>
/// A failure returned from an application service.
/// </summary>
/// <param name="Kind">The category of failure. The API layer maps this to a status code.</param>
/// <param name="Code">
/// A stable, machine-readable identifier such as <c>booking.overlaps_existing</c>. Clients may
/// branch on this; it must not change once released.
/// </param>
/// <param name="Message">A human-readable explanation, safe to show to an API consumer.</param>
public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoDetails =
        new Dictionary<string, string[]>();

    /// <summary>
    /// Per-field problems, keyed by field name. Populated for <see cref="ErrorKind.Validation"/>
    /// and empty otherwise. Maps naturally onto the <c>errors</c> extension of RFC 9457.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Details { get; init; } = NoDetails;

    public static Error NotFound(string code, string message) =>
        new(ErrorKind.NotFound, code, message);

    public static Error Conflict(string code, string message) =>
        new(ErrorKind.Conflict, code, message);

    public static Error Validation(string code, string message) =>
        new(ErrorKind.Validation, code, message);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]> details) =>
        new(ErrorKind.Validation, code, message) { Details = details };

    public static Error Forbidden(string code, string message) =>
        new(ErrorKind.Forbidden, code, message);

    public static Error Unavailable(string code, string message) =>
        new(ErrorKind.Unavailable, code, message);

    public override string ToString() => $"{Kind}/{Code}: {Message}";
}
