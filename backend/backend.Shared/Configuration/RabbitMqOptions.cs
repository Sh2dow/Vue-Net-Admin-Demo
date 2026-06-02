namespace backend.Shared.Configuration;

/// <summary>
/// Configuration options for RabbitMQ messaging.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// RabbitMQ connection URI.
    /// </summary>
    /// <remarks>
    /// Should be set via environment variable in production.
    /// Example: amqp://user:password@localhost:5672
    /// </remarks>
    public string Uri { get; set; } = "amqp://guest:guest@localhost:5672";

    /// <summary>
    /// Main exchange name for events.
    /// </summary>
    public string Exchange { get; init; } = "vue-demo.events";

    /// <summary>
    /// Dead letter exchange name for failed messages.
    /// </summary>
    public string DeadLetterExchange { get; init; } = "vue-demo.events.dlx";

    /// <summary>
    /// Queue name for payment requests.
    /// </summary>
    public string PaymentRequestQueue { get; init; } = "payments.stub.requests";

    /// <summary>
    /// Queue name for order saga messages.
    /// </summary>
    public string OrderSagaQueue { get; init; } = "orders.saga";

    /// <summary>
    /// Queue name for order execution dispatch.
    /// </summary>
    public string OrderExecutionQueue { get; init; } = "orders.execution.dispatch";

    /// <summary>
    /// Batch size for processing integration events.
    /// </summary>
    public int OutboxBatchSize { get; init; } = 20;

    /// <summary>
    /// Delay between retries in seconds.
    /// </summary>
    public int RetryDelaySeconds { get; init; } = 5;
}
