using backend.Domain.Data;
using backend.Infrastructure.Application.Users;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Register MediatR handlers from feature assemblies
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(backend.Tasks.Requests.Tasks.CreateTaskCommand).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(backend.Orders.Requests.Orders.CreateDigitalOrderCommand).Assembly);
        });

        // Database contexts
        var tasksDbConnectionString = configuration.GetConnectionString("Tasks");
        var ordersDbConnectionString = configuration.GetConnectionString("Orders");
        var authDbConnectionString = configuration.GetConnectionString("Auth");

        if (!string.IsNullOrWhiteSpace(tasksDbConnectionString))
        {
            services.AddDbContext<TasksDbContext>(options =>
                options.UseSqlServer(tasksDbConnectionString));
        }

        if (!string.IsNullOrWhiteSpace(ordersDbConnectionString))
        {
            services.AddDbContext<OrdersDbContext>(options =>
                options.UseSqlServer(ordersDbConnectionString));
        }

        if (!string.IsNullOrWhiteSpace(authDbConnectionString))
        {
            services.AddDbContext<AuthDbContext>(options =>
                options.UseSqlServer(authDbConnectionString));
        }

        // User services
        services.AddScoped<IUserDirectory, EfUserDirectory>();
        services.AddScoped<IEffectiveUserAccessor, EffectiveUserAccessor>();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddHttpContextAccessor();

        // Service Bus client (used directly by Functions for publishing)
        var sbConnStr = configuration["ServiceBus:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(sbConnStr))
        {
            services.AddSingleton(_ => new Azure.Messaging.ServiceBus.ServiceBusClient(sbConnStr));
        }
    })
    .Build();

host.Run();
