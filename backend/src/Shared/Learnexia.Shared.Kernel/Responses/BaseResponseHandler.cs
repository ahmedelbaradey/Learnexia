using System.Net;

namespace Learnexia.Shared.Kernel.Responses;

public class BaseResponseHandler
{
    public BaseResponse<T> EmptyCollection<T>(T entity) => new()
    {
        StatusCode = HttpStatusCode.OK,
        Data = entity,
        Message = "Empty Collection",
        Successed = true,
    };

    public BaseResponse<T> Success<T>(T entity) => new()
    {
        StatusCode = HttpStatusCode.OK,
        Successed = true,
        Data = entity,
        Message = "Successfully.",
    };

    public BaseResponse<T> BadRequest<T>(string? message = null) => new()
    {
        StatusCode = HttpStatusCode.BadRequest,
        Successed = false,
        Message = message ?? "Bad Request.",
    };

    public BaseResponse<T> NotFound<T>(string? message = null) => new()
    {
        StatusCode = HttpStatusCode.NotFound,
        Successed = false,
        Message = message ?? "Not Found.",
    };

    public BaseResponse<T> Created<T>(T entity) => new()
    {
        StatusCode = HttpStatusCode.Created,
        Successed = true,
        Data = entity,
        Message = "Added Successed",
    };

    public BaseResponse<T> ServerError<T>(string? message = null) => new()
    {
        StatusCode = HttpStatusCode.InternalServerError,
        Successed = false,
        Message = message ?? "Internal Server Error.",
        Data = default!,
    };

    public BaseResponse<T> Unauthorized<T>(string? message = null) => new()
    {
        StatusCode = HttpStatusCode.Unauthorized,
        Successed = false,
        Message = message ?? "UnAuthorized",
    };

    public BaseResponse<T> BusinessValidation<T>(string? message = null) => new()
    {
        StatusCode = HttpStatusCode.FailedDependency,
        Successed = false,
        Message = message ?? string.Empty,
    };
}
