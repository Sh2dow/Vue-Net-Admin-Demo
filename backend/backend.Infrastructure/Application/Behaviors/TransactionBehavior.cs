using backend.Domain.Data;
using backend.Shared.Application.Abstractions;
using MediatR;

namespace backend.Infrastructure.Application.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly OrdersDbContext _db;

    public TransactionBehavior(OrdersDbContext db)
    {
        _db = db;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction != null)
        {
            return await next();
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var response = await next();
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
