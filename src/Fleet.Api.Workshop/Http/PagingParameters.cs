using Fleet.Common.Paging;

namespace Fleet.Api.Workshop.Http;

internal static class PagingParameters
{
    private static readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase)
        { "page", "pageSize", "sort", "q" };

    public static PageRequest ReadPage(this HttpRequest request) =>
        PageRequest.Of(TryReadInt(request, "page"), TryReadInt(request, "pageSize"));

    public static SortRequest? ReadSort(this HttpRequest request) =>
        SortRequest.Parse(request.Query["sort"]);

    public static FilterRequest ReadFilter(this HttpRequest request)
    {
        var terms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, values) in request.Query)
        {
            if (_reserved.Contains(key)) continue;
            var value = values.ToString();
            if (!string.IsNullOrWhiteSpace(value)) terms[key] = value;
        }
        return new FilterRequest(request.Query["q"], terms);
    }

    private static int? TryReadInt(HttpRequest request, string key) =>
        int.TryParse(request.Query[key], out var value) ? value : null;
}
