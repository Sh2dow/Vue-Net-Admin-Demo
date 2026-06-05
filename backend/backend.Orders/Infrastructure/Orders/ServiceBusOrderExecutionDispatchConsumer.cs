using Azure.Messaging.ServiceBus;
using backend.Domain.Data;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Orders.Infrastructure.Orders;

public sealed class ServiceBusOrderExecutionDispatchConsumer : ServiceBusConsumerBase
{
    private const string Consumer = "order-execution-dispatch-consumer";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderExecutionOptions _executionOptions;
    private readonly ILogger<ServiceBusOrderExecutionDispatchConsumer> _logger;

    public ServiceBusOrderExecutionDispatchConsumer(
        ServiceBusClient client,
        IServiceScopeFactory scopeFactory,
        IOptions<OrderExecutionOptions> executionOptions,
        ILogger<ServiceBusOrderExecutionDispatchConsumer> logger)
        : base(client, logger)
    {
        _scopeFactory = scopeFactory;
        _executionOptions = executionOptions.Value;
        _logger = logger;
    }

    protected override string QueueName => "orders-execution-dispatch";
    protected override string ConsumerName => Consumer;
    protected override IServiceScopeFactory ScopeFactory => _scopeFactory;

    protected override async Task HandleCoreAsync(
        string payload,
        string eventType,
        string messageId,
        IServiceScope scope,
        OrdersDbContext db,
        CancellationToken ct)
    {
        var outbox = scope.ServiceProvider.GetRequiredService<IIntegrationEventOutbox>();

        var dispatched = IntegrationEventSerializer.Deserialize<OrderExecutionDispatchedMessage>(payload);

        _logger.LogInformation(
            "Execution dispatch emitted for order {OrderId} and payment {PaymentId}.",
            dispatched.OrderId,
            dispatched.PaymentId);

        var started = new OrderExecutionStartedMessage(
            dispatched.OrderId,
            dispatched.PaymentId,
            DateTime.UtcNow);

        await outbox.EnqueueAsync(
            IntegrationRoutingKeys.OrderExecutionStarted,
            started,
            dispatched.OrderId.ToString(),
            ct);

        if (_executionOptions.StubDelayMilliseconds > 0)
        {
            await Task.Delay(_executionOptions.StubDelayMilliseconds, ct);
        }

        if (_executionOptions.AutoComplete)
        {
            var completed = new OrderExecutionCompletedMessage(
                dispatched.OrderId,
                dispatched.PaymentId,
                DateTime.UtcNow);

            await outbox.EnqueueAsync(
                IntegrationRoutingKeys.OrderExecutionCompleted,
                completed,
                dispatched.OrderId.ToString(),
                ct);
        }
        else
        {
            var failed = new OrderExecutionFailedMessage(
                dispatched.OrderId,
                dispatched.PaymentId,
                "Stub execution failure.",
                DateTime.UtcNow);

            await outbox.EnqueueAsync(
                IntegrationRoutingKeys.OrderExecutionFailed,
                failed,
                dispatched.OrderId.ToString(),
                ct);
        }
    }
}
