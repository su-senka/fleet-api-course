namespace Fleet.Common.Paging;

/// <summary>
/// The filtering half of a list query: an optional free-text search plus named terms.
/// </summary>
/// <remarks>
/// Modules read the terms they understand and ignore the rest. Silently ignoring an unknown
/// filter is a defensible API choice; rejecting it with a 400 is another. That decision belongs
/// to the endpoint, which is why this type does not make it for you.
/// </remarks>
public sealed record FilterRequest
{
    public static readonly FilterRequest None = new();

    private readonly IReadOnlyDictionary<string, string> _terms;

    public FilterRequest(string? search = null, IReadOnlyDictionary<string, string>? terms = null)
    {
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        _terms = terms ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Free-text search term, already trimmed. <c>null</c> when not supplied.</summary>
    public string? Search { get; }

    public IReadOnlyDictionary<string, string> Terms => _terms;

    public bool IsEmpty => Search is null && _terms.Count == 0;

    /// <summary>The value of a named term, or <c>null</c> when the client did not send it.</summary>
    public string? this[string key] => _terms.TryGetValue(key, out var value) ? value : null;

    /// <summary>Reads a named term as an enum, or <c>null</c> when absent or unparseable.</summary>
    public TEnum? EnumTerm<TEnum>(string key) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(this[key], ignoreCase: true, out var parsed) ? parsed : null;

    /// <summary>Reads a named term as a <see cref="Guid"/>, or <c>null</c> when absent or unparseable.</summary>
    public Guid? GuidTerm(string key) =>
        Guid.TryParse(this[key], out var parsed) ? parsed : null;
}
