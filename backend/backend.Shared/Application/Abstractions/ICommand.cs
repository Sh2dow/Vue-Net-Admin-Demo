using MediatR;

namespace backend.Shared.Application.Abstractions;

public interface ICommand<out TResponse> : IRequest<TResponse>;
