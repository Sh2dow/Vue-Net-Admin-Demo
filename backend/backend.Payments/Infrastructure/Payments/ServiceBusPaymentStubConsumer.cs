using Azure.Messaging.ServiceBus;
using backend.Domain.Data;
using backend.Domain.Models;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Messaging.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Payments.Infrastructure.Payments;

public sealed class ServiceBusPaymentStubConsumer : ServiceBusConsumerBase
{
    private const string Consumer = "payment-stub-consumer";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PaymentOptions _paymentOptions;
    private readonly ILogger<ServiceBusPaymentStubConsumer> _logger;

    public ServiceBusPaymentStubConsumer(
        ServiceBusClient client,
        IServiceScopeFactory scopeFactory,
        IOptions<PaymentOptions> paymentOptions,
        ILogger<ServiceBusPaymentStubConsumer> logger)
        : base(client, logger)
    {
        _scopeFactory = scopeFactory;
        _paymentOptions = paymentOptions.Value;
        _logger = logger;
    }

    protected override string QueueName => "payments-stub-requests";
    protected override string ConsumerName => Consumer;
    protected override IServiceScopeFactory ScopeFactory => _scopeFactory;

    protected override async Task HandleCoreAsync(
        string payload,
        string eventType,
        string messageId,
        IServiceScope scope,
        OrdersDbContext ordersDb,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "ServiceBusPaymentStubConsumer processing message: {MessageId}, Type: {EventType}",
            messageId, eventType);

        var paymentsDb = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IIntegrationEventOutbox>();

        var orderPaymentRequested = IntegrationEventSerializer.Deserialize<OrderPaymentRequestedMessage>(payload);

        _logger.LogInformation(
            "Processing payment for order: {OrderId}, Amount: {Amount}",
            orderPaymentRequested.OrderId, orderPaymentRequested.TotalAmount);

        var currentAttemptNumber = await paymentsDb.PaymentEventRecords
            .Where(x => x.OrderId == orderPaymentRequested.OrderId)
            .MaxAsync(x => (int?)x.AttemptNumber, ct) ?? 0;

        var paymentId = Guid.NewGuid();
        var attemptNumber = currentAttemptNumber + 1;
        const int initiatedSequence = 1;
        const int finalSequence = 2;

        var initiated = new PaymentInitiatedMessage(
            paymentId,
            orderPaymentRequested.OrderId,
            orderPaymentRequested.TotalAmount,
            DateTime.UtcNow);

        paymentsDb.PaymentEventRecords.Add(new PaymentEventRecord
        {
            PaymentId = paymentId,
            OrderId = orderPaymentRequested.OrderId,
            AttemptNumber = attemptNumber,
            SequenceNumber = initiatedSequence,
            EventType = nameof(PaymentInitiatedMessage),
            Data = IntegrationEventSerializer.Serialize(initiated),
            OccurredAtUtc = initiated.OccurredAtUtc
        });

        await outbox.EnqueueAsync(
            IntegrationRoutingKeys.PaymentInitiated,
            initiated,
            orderPaymentRequested.OrderId.ToString(),
            ct);

        if (_paymentOptions.StubDelayMilliseconds > 0)
        {
            await Task.Delay(_paymentOptions.StubDelayMilliseconds, ct);
        }

        if (_paymentOptions.AutoAuthorize)
        {
            var authorized = new PaymentAuthorizedMessage(
                paymentId,
                orderPaymentRequested.OrderId,
                orderPaymentRequested.TotalAmount,
                DateTime.UtcNow);

            paymentsDb.PaymentEventRecords.Add(new PaymentEventRecord
            {
                PaymentId = paymentId,
                OrderId = orderPaymentRequested.OrderId,
                AttemptNumber = attemptNumber,
                SequenceNumber = finalSequence,
                EventType = nameof(PaymentAuthorizedMessage),
                Data = IntegrationEventSerializer.Serialize(authorized),
                OccurredAtUtc = authorized.OccurredAtUtc
            });

            await outbox.EnqueueAsync(
                IntegrationRoutingKeys.PaymentAuthorized,
                authorized,
                orderPaymentRequested.OrderId.ToString(),
                ct);
        }
        else
        {
            var failed = new PaymentFailedMessage(
                paymentId,
                orderPaymentRequested.OrderId,
                "Stub payment rejection.",
                DateTime.UtcNow);

            paymentsDb.PaymentEventRecords.Add(new PaymentEventRecord
            {
                PaymentId = paymentId,
                OrderId = orderPaymentRequested.OrderId,
                AttemptNumber = attemptNumber,
                SequenceNumber = finalSequence,
                EventType = nameof(PaymentFailedMessage),
                Data = IntegrationEventSerializer.Serialize(failed),
                OccurredAtUtc = failed.OccurredAtUtc
            });

            await outbox.EnqueueAsync(
                IntegrationRoutingKeys.PaymentFailed,
                failed,
                orderPaymentRequested.OrderId.ToString(),
                ct);
        }

        await paymentsDb.SaveChangesAsync(ct);
    }
}
