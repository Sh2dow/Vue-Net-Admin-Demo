using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var environmentName = builder.Environment.EnvironmentName;

if (string.Equals(environmentName, "Aws", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("The 'aws' AppHost profile starts the services on this machine with AWS-oriented configuration. Use scripts/deploy.sh for EC2 deployment.");
}

var api = builder.AddProject<Projects.backend_Api>("api");
ApplyCommonEnvironment(api);
SetRequiredEnvironment(api, "RabbitMq__Uri", "ConnectionStrings:messaging");
SetRequiredEnvironment(api, "Auth__Authority", "Auth:Authority");
SetRequiredEnvironment(api, "AuthService__BaseUrl", "AuthService:BaseUrl");
SetRequiredEnvironment(api, "DownstreamServices__UsersBaseUrl", "DownstreamServices:UsersBaseUrl");
SetRequiredEnvironment(api, "DownstreamServices__TasksBaseUrl", "DownstreamServices:TasksBaseUrl");
SetRequiredEnvironment(api, "DownstreamServices__OrdersBaseUrl", "DownstreamServices:OrdersBaseUrl");
SetRequiredEnvironment(api, "DownstreamServices__PaymentsBaseUrl", "DownstreamServices:PaymentsBaseUrl");
SetOptionalEnvironment(api, "ASPNETCORE_URLS", "AppHost:ServiceBindings:Api");

var authApi = builder.AddProject<Projects.backend_Auth_Api>("auth-api");
ApplyCommonEnvironment(authApi);
SetRequiredEnvironment(authApi, "ConnectionStrings__Auth", "ConnectionStrings:Auth");
SetOptionalEnvironment(authApi, "RabbitMq__Uri", "ConnectionStrings:messaging");
SetOptionalEnvironment(authApi, "ASPNETCORE_URLS", "AppHost:ServiceBindings:AuthApi");

var usersApi = builder.AddProject<Projects.backend_Users_Api>("users-api");
ApplyCommonEnvironment(usersApi);
SetRequiredEnvironment(usersApi, "ConnectionStrings__Auth", "ConnectionStrings:Auth");
SetOptionalEnvironment(usersApi, "ConnectionStrings__Orders", "ConnectionStrings:Orders");
SetOptionalEnvironment(usersApi, "RabbitMq__Uri", "ConnectionStrings:messaging");
SetOptionalEnvironment(usersApi, "RabbitMq__Enabled", "RabbitMq:Enabled");
SetOptionalEnvironment(usersApi, "ASPNETCORE_URLS", "AppHost:ServiceBindings:UsersApi");

var tasksApi = builder.AddProject<Projects.backend_Tasks_Api>("tasks-api");
ApplyCommonEnvironment(tasksApi);
SetRequiredEnvironment(tasksApi, "ConnectionStrings__Tasks", "ConnectionStrings:Tasks");
SetRequiredEnvironment(tasksApi, "ConnectionStrings__Auth", "ConnectionStrings:Auth");
SetRequiredEnvironment(tasksApi, "RabbitMq__Uri", "ConnectionStrings:messaging");
SetRequiredEnvironment(tasksApi, "Auth__Authority", "Auth:Authority");
SetOptionalEnvironment(tasksApi, "RabbitMq__Enabled", "RabbitMq:Enabled");
SetOptionalEnvironment(tasksApi, "ASPNETCORE_URLS", "AppHost:ServiceBindings:TasksApi");

var ordersApi = builder.AddProject<Projects.backend_Orders_Api>("orders-api");
ordersApi.WithReference(authApi);
ApplyCommonEnvironment(ordersApi);
SetRequiredEnvironment(ordersApi, "ConnectionStrings__Orders", "ConnectionStrings:Orders");
SetRequiredEnvironment(ordersApi, "ConnectionStrings__Payments", "ConnectionStrings:Payments");
SetRequiredEnvironment(ordersApi, "ConnectionStrings__Auth", "ConnectionStrings:Auth");
SetRequiredEnvironment(ordersApi, "RabbitMq__Uri", "ConnectionStrings:messaging");
SetRequiredEnvironment(ordersApi, "Auth__Authority", "Auth:Authority");
SetRequiredEnvironment(ordersApi, "AuthService__BaseUrl", "AuthService:BaseUrl");
SetOptionalEnvironment(ordersApi, "RabbitMq__Enabled", "RabbitMq:Enabled");
SetOptionalEnvironment(ordersApi, "ASPNETCORE_URLS", "AppHost:ServiceBindings:OrdersApi");

var paymentsApi = builder.AddProject<Projects.backend_Payments_Api>("payments-api");
ApplyCommonEnvironment(paymentsApi);
SetRequiredEnvironment(paymentsApi, "ConnectionStrings__Orders", "ConnectionStrings:Orders");
SetRequiredEnvironment(paymentsApi, "ConnectionStrings__Payments", "ConnectionStrings:Payments");
SetRequiredEnvironment(paymentsApi, "ConnectionStrings__Auth", "ConnectionStrings:Auth");
SetRequiredEnvironment(paymentsApi, "RabbitMq__Uri", "ConnectionStrings:messaging");
SetOptionalEnvironment(paymentsApi, "RabbitMq__Enabled", "RabbitMq:Enabled");
SetOptionalEnvironment(paymentsApi, "ASPNETCORE_URLS", "AppHost:ServiceBindings:PaymentsApi");

builder.Build().Run();

void ApplyCommonEnvironment(IResourceBuilder<ProjectResource> project)
{
    project.WithEnvironment("DOTNET_ENVIRONMENT", environmentName);
    project.WithEnvironment("ASPNETCORE_ENVIRONMENT", environmentName);
}

void SetRequiredEnvironment(IResourceBuilder<ProjectResource> project, string environmentVariableName, string configurationKey)
{
    var value = configuration[configurationKey];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"Missing required AppHost configuration '{configurationKey}' for environment variable '{environmentVariableName}'.");
    }

    project.WithEnvironment(environmentVariableName, value);
}

void SetOptionalEnvironment(IResourceBuilder<ProjectResource> project, string environmentVariableName, string configurationKey)
{
    var value = configuration[configurationKey];
    if (!string.IsNullOrWhiteSpace(value))
    {
        project.WithEnvironment(environmentVariableName, value);
    }
}
