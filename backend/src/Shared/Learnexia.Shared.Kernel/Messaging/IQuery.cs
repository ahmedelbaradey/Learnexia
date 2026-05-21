using MediatR;

namespace Learnexia.Shared.Kernel.Messaging;

public interface IQuery<out TResponse> : IRequest<TResponse>
{
}
