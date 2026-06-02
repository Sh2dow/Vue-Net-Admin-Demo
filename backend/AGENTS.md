# AGENTS.md — Async/Await & Concurrency Rules (.NET)

## 1. Always Use CancellationToken

**Problem:** Requests continue running after client disconnects → wasted CPU & memory.
**Rule:** Always pass `CancellationToken` through the entire async chain.

```csharp
public async Task<Order> GetOrderAsync(int id, CancellationToken ct)
{
    return await _db.Orders
        .Where(o => o.Id == id)
        .FirstOrDefaultAsync(ct);
}
```

---

## 2. Never Block on Async Code

**Problem:** `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` → Thread pool starvation → deadlocks.
**Rule:** Use `await` all the way.

❌ Bad:

```csharp
var result = service.GetDataAsync().Result;
```

✅ Good:

```csharp
var result = await service.GetDataAsync();
```

---

## 3. Always Set Timeouts for Outbound Calls

**Problem:** HttpClient hangs forever → resource exhaustion.
**Rule:** Use `Timeout` + `CancellationTokenSource`.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var response = await _httpClient.GetAsync(url, cts.Token);
```

---

## 4. Async All the Way Down

**Problem:** Mixing sync and async → deadlocks & poor scalability.
**Rule:** Entire chain must be async:

```
Controller → Service → Repository → Database
```

---

## 5. Do Not Use Task.Run for I/O

**Problem:** Wastes thread pool threads and adds context switching.
**Rule:** Use `Task.Run` **only for CPU-bound work**.

❌ Bad:

```csharp
await Task.Run(() => _db.Orders.ToList());
```

✅ Good:

```csharp
await _db.Orders.ToListAsync();
```

---

## 6. Stream Large Payloads

**Problem:** Loading huge files into memory → OutOfMemoryException.
**Rule:** Use streaming.

```csharp
await using var stream = await response.Content.ReadAsStreamAsync(ct);
await stream.CopyToAsync(fileStream, ct);
```

---

## 7. Limit Concurrency

**Problem:** `Task.WhenAll` with 1000 tasks → API rate limits & cascade failures.
**Rule:** Use batching or `Parallel.ForEachAsync`.

```csharp
await Parallel.ForEachAsync(items, new ParallelOptions
{
    MaxDegreeOfParallelism = 10
}, async (item, ct) =>
{
    await ProcessItemAsync(item, ct);
});
```

---

## 8. Avoid async void

**Problem:** Unhandled exceptions crash the process.
**Rule:** Use `Task` or `Task<T>`.

❌ Bad:

```csharp
public async void ProcessAsync()
```

✅ Good:

```csharp
public async Task ProcessAsync()
```

---

## 9. No Fire-and-Forget Tasks

**Problem:** Silent failures & memory leaks.
**Rule:** Use `BackgroundService`, queues, or Outbox pattern.

```csharp
public class Worker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await ProcessQueue(ct);
        }
    }
}
```

---

## 10. Use ConfigureAwait(false) in Libraries

**Problem:** Capturing synchronization context unnecessarily.
**Rule:** Use in library code.

```csharp
await SomeOperationAsync().ConfigureAwait(false);
```

---

## Summary Rule

> **Async is not about speed — it's about scalability.**
> The goal is to **free threads while waiting for I/O**.

---

# AGENTS.md — .NET Engineering Rules

## Section 1 — EF Core Rules

### 1. Always Use AsNoTracking for Read-Only Queries

**Why:** ChangeTracker is expensive.

```csharp
var users = await _db.Users
    .AsNoTracking()
    .Where(x => x.IsActive)
    .ToListAsync(ct);
```

Use when:

* Read-only queries
* Lists
* API GET endpoints

---

### 2. Avoid N+1 Queries

**Problem:** Lazy loading causes 100 extra queries.

❌ Bad:

```csharp
var orders = await _db.Orders.ToListAsync();
foreach (var order in orders)
    Console.WriteLine(order.Customer.Name);
```

✅ Good:

```csharp
var orders = await _db.Orders
    .Include(o => o.Customer)
    .ToListAsync();
```

---

### 3. Use Select Instead of Include When Possible

**Best performance** → projection.

```csharp
var orders = await _db.Orders
    .Select(o => new OrderDto
    {
        Id = o.Id,
        CustomerName = o.Customer.Name
    })
    .ToListAsync();
```

---

### 4. Never Return IQueryable From Repository

**Why:** Breaks encapsulation and can execute queries outside your layer.

Return:

* `Task<List<T>>`
* `Task<T>`
* `IAsyncEnumerable<T>`

---

### 5. Use Transactions When Multiple SaveChanges

```csharp
await using var tx = await _db.Database.BeginTransactionAsync(ct);

await _db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

---

### 6. Prefer TPH for Inheritance

| Strategy | Performance | Notes            |
| -------- | ----------- | ---------------- |
| TPH      | Fastest     | No joins         |
| TPT      | Slow        | Many joins       |
| TPC      | Medium      | Data duplication |

**Default → TPH**

---

## Section 2 — ASP.NET Core Rules

### 1. Middleware Order Matters

Correct order:

```csharp
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
```

---

### 2. Do Not Put Business Logic in Controllers

Controller = HTTP layer only.

```csharp
[HttpPost]
public async Task<IActionResult> CreateOrder(CreateOrderRequest request, CancellationToken ct)
{
    var result = await _service.CreateOrder(request, ct);
    return Ok(result);
}
```

---

### 3. Use IHttpClientFactory

**Never create HttpClient manually.**

```csharp
services.AddHttpClient<IEmailService, EmailService>();
```

---

### 4. Use ProblemDetails for Errors

```csharp
return Problem("Order not found", statusCode: 404);
```

---

### 5. Logging Must Include CorrelationId

For microservices debugging.

---

## Section 3 — Microservices Rules

### 1. Each Service Owns Its Database

**Never share database between services.**

---

### 2. Communication Rules

| Type     | Technology           |
| -------- | -------------------- |
| Sync     | HTTP REST            |
| Async    | RabbitMQ / Kafka     |
| Realtime | WebSockets / SignalR |

---

### 3. Use Saga Instead of Distributed Transactions (2PC)

**2PC = bad for microservices**

Use:

* Choreography (events)
* Orchestration (Saga)

---

### 4. Services Must Be Stateless

State stored in:

* Database
* Redis
* Cache
* Message Queue

---

### 5. Always Have Retry + Circuit Breaker

Use **Polly** in .NET.

```csharp
services.AddHttpClient("api")
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());
```

---

## Section 4 — RabbitMQ Rules

### 1. Always Use At-Least-Once Delivery

Meaning:

* Messages can be duplicated
* Consumers must be **idempotent**

Check:

```csharp
if (alreadyProcessed)
{
    await channel.BasicAckAsync(tag);
    return;
}
```

You already implemented this correctly in your Outbox consumer 👍

---

### 2. Never Use AutoAck

```csharp
autoAck: false
```

---

### 3. Use Outbox Pattern

Flow:

```
DB Transaction:
    Save Order
    Save Outbox Event
Commit

Background Worker:
    Send event to RabbitMQ
```

This guarantees **no lost messages**.

---

### 4. Use Dead Letter Queue (DLQ)

For failed messages after retries.

---

### 5. Version Your Messages

```json
{
  "version": 2,
  "orderId": "..."
}
```

---

## Section 5 — Distributed Systems Rules

### Golden Rules:

| Problem           | Solution             |
| ----------------- | -------------------- |
| Service down      | Retry                |
| Service slow      | Timeout              |
| Service failing   | Circuit Breaker      |
| Message lost      | Outbox               |
| Duplicate message | Idempotency          |
| Long transaction  | Saga                 |
| High load         | Queue                |
| Fast reads        | Cache                |
| Data consistency  | Eventual consistency |



---

## Section 6 — Dependency Injection & Hosted Services

### 1. Avoid Captive Dependencies

**Problem:** A longer-lived service captures a shorter-lived one.

The most common case is a Singleton holding a Scoped `DbContext`.

**Rule:** Never inject Scoped services directly into Singleton services. Create a scope per unit of work and resolve Scoped dependencies inside that scope.

```csharp
public sealed class Worker
{
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.SaveChangesAsync(ct);
    }
}
```

---

### 2. IHostedService and BackgroundService Are Singletons

**Problem:** Hosted services live for the entire app lifetime, so injecting Scoped services directly creates invalid lifetimes and unstable behavior.

**Rule:** In `IHostedService` and `BackgroundService`, always use `IServiceScopeFactory.CreateScope()` or `CreateAsyncScope()` for each unit of work.

```csharp
public sealed class QueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public QueueWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();

            await handler.ProcessAsync(stoppingToken);
        }
    }
}
```
