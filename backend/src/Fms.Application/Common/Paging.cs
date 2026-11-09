using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record PageQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Search { get; init; }

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize is < 1 or > 200 ? 25 : PageSize;
}

public sealed record DateRangeQuery : PageQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? PartyId { get; init; }
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, PageQuery page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.SafePage - 1) * page.SafePageSize).Take(page.SafePageSize).ToListAsync(ct);
        return new PagedResult<T>(items, total, page.SafePage, page.SafePageSize);
    }

    /// <summary>Normalised search term (lower-case, trimmed) or null when empty.</summary>
    public static string? NormalizedSearch(this PageQuery query) =>
        string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().ToLowerInvariant();
}
