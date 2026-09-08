namespace Fleet.Common.Paging;

/// <summary>
/// A one-based page selection.
/// </summary>
/// <remarks>
/// Modules take this type rather than two loose integers so that "page 0" and "page size 5000"
/// cannot reach a query. Use <see cref="Of"/>; the constructor is not the intended entry point.
/// Rejecting a bad page number with a 400 is still the API layer's decision, not this type's.
/// </remarks>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>The first page, at the default size. Handy as a parameter default.</summary>
    public static readonly PageRequest First = new(1, DefaultPageSize);

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>One-based page number.</summary>
    public int Page { get; }

    public int PageSize { get; }

    /// <summary>Rows to skip. Convenient for <c>IQueryable.Skip</c>.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>Rows to take. Convenient for <c>IQueryable.Take</c>.</summary>
    public int Take => PageSize;

    /// <summary>
    /// Builds a page request, clamping both values into range. <c>null</c> means "use the default".
    /// </summary>
    public static PageRequest Of(int? page, int? pageSize)
    {
        var safePage = page is null or < 1 ? 1 : page.Value;
        var safeSize = pageSize switch
        {
            null or < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize.Value,
        };

        return new PageRequest(safePage, safeSize);
    }
}
