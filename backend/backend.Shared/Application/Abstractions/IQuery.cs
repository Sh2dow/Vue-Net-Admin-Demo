using MediatR;

namespace backend.Shared.Application.Abstractions;

public interface IQuery<out TResponse> : IRequest<TResponse>;
