namespace UserManagementAPI.Models;

/// <summary>
/// Envelope for paginated list responses. Added while fixing a
/// performance bottleneck in GET /api/users: returning every row with no
/// bound doesn't scale as TechHive's user table grows, so callers now get
/// a page at a time plus enough metadata to fetch the rest.
/// </summary>
/// <typeparam name="T">The item type being paged.</typeparam>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
