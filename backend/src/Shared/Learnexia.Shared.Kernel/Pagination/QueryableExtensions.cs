using System.Linq.Dynamic.Core;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Shared.Kernel.Pagination;

public static class QueryableExtensions
{
    // NOTE: backend/ routed orderBy through a custom OrderQueryBuilder normalizer; this uses
    // System.Linq.Dynamic.Core directly. Behavior matches for plain "prop [asc|desc]" strings.
    public static IQueryable<T> Sort<T>(this IQueryable<T> data, string? orderByQueryString)
    {
        if (string.IsNullOrWhiteSpace(orderByQueryString))
            return data;

        return data.OrderBy(orderByQueryString);
    }

    public static Task<PaginatedResult<T>> ToPaginatedListAsync<T>(this IQueryable<T> source, int pageNumber, int pageSize, string? orderBy)
        where T : class
    {
        if (source == null)
            throw new Exception("Source is empty.");

        pageNumber = pageNumber == 0 ? 1 : pageNumber;
        pageSize = pageSize == 0 ? 200 : pageSize;
        var count = source.Count();
        if (count == 0)
            return Task.FromResult(PaginatedResult<T>.Success(new List<T>(), count, pageNumber, pageSize));

        pageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var items = source.Sort(orderBy).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(PaginatedResult<T>.Success(items, count, pageNumber, pageSize));
    }
}
