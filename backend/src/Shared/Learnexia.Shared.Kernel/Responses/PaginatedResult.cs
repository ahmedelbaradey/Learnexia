using System.Net;

namespace Learnexia.Shared.Kernel.Responses;

public class PaginatedResult<T> : BaseResponse<List<T>>
{
    public int CurrentPage { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }

    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public PaginatedResult()
    {
        Successed = true;
    }

    public PaginatedResult(List<T> data, int totalCount, int page, int pageSize, string? message = null) : base(data, message)
    {
        TotalCount = totalCount;
        CurrentPage = page;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        Successed = true;
        StatusCode = HttpStatusCode.OK;
    }

    public PaginatedResult(string message, bool succeeded = false) : base(message, succeeded)
    {
        Data = new List<T>();
    }

    public static PaginatedResult<T> Success(List<T> data, int count, int page, int pageSize, string? message = null)
        => new(data, count, page, pageSize, message);

    public static PaginatedResult<T> ServerError(string? message = null) => new()
    {
        StatusCode = HttpStatusCode.InternalServerError,
        Successed = false,
        Message = message ?? "Internal Server Error.",
    };

    public static PaginatedResult<T> EmptyCollection(string? message = "Empty Collection") => new()
    {
        StatusCode = HttpStatusCode.OK,
        Message = message,
        Successed = true,
    };
}
