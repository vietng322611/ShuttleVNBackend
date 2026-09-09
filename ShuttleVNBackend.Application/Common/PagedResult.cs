namespace ShuttleVNBackend.Application.Common;

public sealed record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPage => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
