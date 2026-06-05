using System.Text;
using Azure.Messaging.ServiceBus;
using backend.Domain.Data;
using backend.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace backend.Infrastructure.Infrastructure.Messaging;

public abstract class ServiceBusConsumerBase : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly ILogger _logger;

    protected abstract string QueueName { get; }
    protected abstract string ConsumerName { get; }
    protected abstract IServiceScopeFactory ScopeFactory { get; }

    protected ServiceBusConsumerBase(ServiceBusClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "{Consumer} starting. Queue: {Queue}", ConsumerName, QueueName);

        var processor = _client.CreateProcessor(QueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });

        processor.ProcessMessageAsync += async args =>
        {
            await ProcessMessageAsync(args, stoppingToken);
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception,
                "{Consumer} Service Bus processor error", ConsumerName);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation("{Consumer} started processing", ConsumerName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        await processor.StopProcessingAsync();
        await processor.DisposeAsync();
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args, CancellationToken ct)
    {
        var messageId = args.Message.MessageId ?? Guid.NewGuid().ToString("N");
        var eventType = args.Message.Subject ?? "unknown";

        _logger.LogInformation(
            "{Consumer} received message: {MessageId}, Type: {EventType}",
            ConsumerName, messageId, eventType);

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

            var alreadyProcessed = await db.ConsumedMessages
                .AnyAsync(x => x.Consumer == ConsumerName && x.MessageId == messageId, ct);

            if (alreadyProcessed)
            {
                _logger.LogInformation(
                    "Message already processed: {MessageId}", messageId);
                await args.CompleteMessageAsync(args.Message, ct);
                return;
            }

            var payload = Encoding.UTF8.GetString(args.Message.Body.ToArray());

            await HandleCoreAsync(payload, eventType, messageId, scope, db, ct);

            db.ConsumedMessages.Add(new ConsumedMessage
            {
                Consumer = ConsumerName,
                MessageId = messageId
            });

            await db.SaveChangesAsync(ct);
            await args.CompleteMessageAsync(args.Message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "{Consumer} failed to handle message {MessageId}.",
                ConsumerName, messageId);
            await args.AbandonMessageAsync(args.Message, cancellationToken: ct);
        }
    }

    protected abstract Task HandleCoreAsync(
        string payload,
        string eventType,
        string messageId,
        IServiceScope scope,
        OrdersDbContext db,
        CancellationToken ct);
}
