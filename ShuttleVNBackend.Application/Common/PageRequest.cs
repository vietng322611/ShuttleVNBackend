namespace ShuttleVNBackend.Application.Common;

public record PageRequest
{
    public const int MaxPageSize = 100;

    public PageRequest(int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public int PageNumber
    {
        get;
        private init => field = Math.Max(1, value);
    } = 1;

    public int PageSize
    {
        get;
        private init => field = Math.Clamp(value, 1, MaxPageSize);
    } = 20;
}