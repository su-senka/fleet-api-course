namespace Fleet.Common.Paging;

public enum SortDirection
{
    Ascending = 0,
    Descending = 1,
}

/// <summary>
/// A single sort instruction, parsed from a query string value such as <c>plate</c>,
/// <c>-plate</c> or <c>plate:desc</c>.
/// </summary>
/// <remarks>
/// <see cref="Field"/> is whatever the client sent. It is <em>not</em> safe to concatenate into
/// SQL or to hand to a dynamic-LINQ helper. Every module maps the field name through an explicit
/// allow-list of sortable columns; see the Vehicles module for the worked example.
/// </remarks>
public sealed record SortRequest(string Field, SortDirection Direction)
{
    /// <summary>
    /// Parses a sort expression. Returns <c>null</c> for null, empty or whitespace input, which
    /// callers should read as "no sort requested, use your default order".
    /// </summary>
    public static SortRequest? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();

        if (text.StartsWith('-'))
        {
            return new SortRequest(text[1..].Trim(), SortDirection.Descending);
        }

        var separator = text.IndexOf(':');
        if (separator < 0)
        {
            return new SortRequest(text, SortDirection.Ascending);
        }

        var field = text[..separator].Trim();
        var direction = text[(separator + 1)..].Trim();

        return new SortRequest(
            field,
            direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? SortDirection.Descending
                : SortDirection.Ascending);
    }
}
