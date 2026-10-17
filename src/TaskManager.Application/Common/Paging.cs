namespace TaskManager.Application.Common;

/// <summary>ページングされた一覧（docs/design/api-design.md §1.1）。</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static PagedResponse<T> Create(IReadOnlyList<T> items, PageRequest page, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(page);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)page.PageSize);
        return new PagedResponse<T>(items, page.Page, page.PageSize, totalCount, totalPages);
    }
}

/// <summary>ページ指定（1 始まり）。</summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public int Skip => (Page - 1) * PageSize;

    public static PageRequest Of(int? page, int? pageSize, int defaultPageSize, int maxPageSize = 100)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var size = pageSize is null or < 1 ? defaultPageSize : Math.Min(pageSize.Value, maxPageSize);
        return new PageRequest(p, size);
    }
}
