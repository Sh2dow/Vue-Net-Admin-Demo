using Azure.Messaging.ServiceBus;
using backend.Shared.Application.Messaging;

namespace backend.Infrastructure.Infrastructure.Messaging;

public sealed class ServiceBusOutboxPublisher : IOutboxPublisher
{
    private static readonly Dictionary<string, string[]> RoutingTable = new()
    {
        [IntegrationRoutingKeys.OrderPaymentRequested] = ["payments-stub-requests", "orders-saga"],
        [IntegrationRoutingKeys.PaymentInitiated] = ["orders-saga"],
        [IntegrationRoutingKeys.PaymentAuthorized] = ["orders-saga"],
        [IntegrationRoutingKeys.PaymentFailed] = ["orders-saga"],
        [IntegrationRoutingKeys.OrderExecutionDispatched] = ["orders-execution-dispatch", "orders-saga"],
        [IntegrationRoutingKeys.OrderExecutionStarted] = ["orders-saga"],
        [IntegrationRoutingKeys.OrderExecutionCompleted] = ["orders-saga"],
        [IntegrationRoutingKeys.OrderExecutionFailed] = ["orders-saga"],
    };

    private readonly ServiceBusClient _client;

    public ServiceBusOutboxPublisher(ServiceBusClient client)
    {
        _client = client;
    }

    public Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public async Task PublishAsync(
        string routingKey,
        ReadOnlyMemory<byte> body,
        string? eventType,
        string? correlationId,
        string? messageId,
        CancellationToken ct = default)
    {
        if (!RoutingTable.TryGetValue(routingKey, out var queueNames))
        {
            return;
        }

        foreach (var queueName in queueNames)
        {
            var sender = _client.CreateSender(queueName);
            await using (sender.ConfigureAwait(false))
            {
                var message = new ServiceBusMessage(body)
                {
                    MessageId = messageId,
                    CorrelationId = correlationId,
                    ContentType = "application/json",
                    Subject = eventType
                };

                await sender.SendMessageAsync(message, ct);
            }
        }
    }
}
