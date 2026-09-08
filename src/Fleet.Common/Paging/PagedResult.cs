namespace Fleet.Common.Paging;

/// <summary>
/// One page of results plus the counts a client needs to render a pager.
/// </summary>
/// <remarks>
/// <see cref="TotalCount"/> costs an extra <c>COUNT(*)</c> round trip. That is the right trade
/// for a fleet-sized dataset and the wrong one for an infinite feed; keyset pagination is the
/// alternative worth reading about once offset pagination makes sense.
/// </remarks>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount)
{
    public static PagedResult<T> Empty(PageRequest request) =>
        new([], request.Page, request.PageSize, 0);

    public int TotalPages => PageSize <= 0 ? 0 : (int)((TotalCount + PageSize - 1) / PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    /// <summary>Projects each item, keeping the paging counts. Useful for entity-to-DTO mapping.</summary>
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
        new([.. Items.Select(map)], Page, PageSize, TotalCount);
}
