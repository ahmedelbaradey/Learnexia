using System.Net;

namespace Learnexia.Shared.Kernel.Responses;

public class BaseResponse<T>
{
    public HttpStatusCode StatusCode { get; set; }
    public bool Successed { get; set; }
    public string? Message { get; set; }
    public T Data { get; set; } = default!;
    public IReadOnlyList<object> Errors { get; set; } = new List<object>();

    public BaseResponse()
    {
    }

    public BaseResponse(T data, string? message = null)
    {
        Successed = true;
        Data = data;
        Message = message;
        StatusCode = HttpStatusCode.OK;
    }

    public BaseResponse(string message)
    {
        Successed = false;
        Message = message;
    }

    public BaseResponse(string message, bool successed)
    {
        Message = message;
        Successed = successed;
    }
}
