using MediatR;

namespace Learnexia.Shared.Kernel.Messaging;

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
