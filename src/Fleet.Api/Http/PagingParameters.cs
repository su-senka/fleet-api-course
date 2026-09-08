using Fleet.Common.Paging;

namespace Fleet.Api.Http;

/// <summary>
/// Reads the paging, sorting and filtering conventions out of a query string.
/// </summary>
/// <remarks>
/// <para>
/// One place, so that every collection endpoint behaves the same way. A client that has learned
/// <c>?page=2&amp;pageSize=50&amp;sort=-startsAt</c> on one resource should not have to learn
/// something else on the next.
/// </para>
/// <para>
/// Out-of-range values are clamped rather than rejected - <see cref="PageRequest.Of"/> does the
/// clamping. Returning a 400 for <c>pageSize=1000</c> is equally defensible; silently capping it
/// keeps naive clients working, at the cost of them never learning the limit.
/// </para>
/// </remarks>
internal static class PagingParameters
{
    /// <summary>Query-string keys that are paging, not filtering, and so are not passed on as terms.</summary>
    private static readonly HashSet<string> Reserved =
        new(StringComparer.OrdinalIgnoreCase) { "page", "pageSize", "sort", "q" };

    public static PageRequest ReadPage(this HttpRequest request) =>
        PageRequest.Of(
            TryReadInt(request, "page"),
            TryReadInt(request, "pageSize"));

    public static SortRequest? ReadSort(this HttpRequest request) =>
        SortRequest.Parse(request.Query["sort"]);

    /// <summary>
    /// Everything else in the query string becomes a filter term, with <c>q</c> as the free-text
    /// search. A module reads the terms it understands and ignores the rest.
    /// </summary>
    public static FilterRequest ReadFilter(this HttpRequest request)
    {
        var terms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, values) in request.Query)
        {
            if (Reserved.Contains(key))
            {
                continue;
            }

            var value = values.ToString();

            if (!string.IsNullOrWhiteSpace(value))
            {
                terms[key] = value;
            }
        }

        return new FilterRequest(request.Query["q"], terms);
    }

    private static int? TryReadInt(HttpRequest request, string key) =>
        int.TryParse(request.Query[key], out var value) ? value : null;
}
