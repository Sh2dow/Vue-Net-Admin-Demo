# AWS AppHost Profile

The `aws` launch profile in [launchSettings.json](/backend/backend.AppHost/Properties/launchSettings.json) runs `backend.AppHost` with `DOTNET_ENVIRONMENT=Aws`.

This profile is local-only orchestration. It starts child processes on the machine where you run `dotnet run`. It does not provision EC2, push containers, or deploy anything to AWS.

For actual AWS deployment, use [deploy.sh](/scripts/deploy.sh), which provisions EC2/RDS and starts the Docker Compose stack on the instance.

`appsettings.Aws.json` only provides non-secret runtime defaults:

- internal service bindings
- internal downstream base URLs
- internal authority
- RabbitMQ enabled flag

You still need to provide these settings before launching the profile:

- `ConnectionStrings__Auth`
- `ConnectionStrings__Tasks`
- `ConnectionStrings__Orders`
- `ConnectionStrings__Payments`
- `ConnectionStrings__messaging`

Optional overrides:

- `AuthService__BaseUrl`
- `DownstreamServices__UsersBaseUrl`
- `DownstreamServices__TasksBaseUrl`
- `DownstreamServices__OrdersBaseUrl`
- `DownstreamServices__PaymentsBaseUrl`
- `AppHost__ServiceBindings__Api`
- `AppHost__ServiceBindings__AuthApi`
- `AppHost__ServiceBindings__UsersApi`
- `AppHost__ServiceBindings__TasksApi`
- `AppHost__ServiceBindings__OrdersApi`
- `AppHost__ServiceBindings__PaymentsApi`

Example launch command:

```bash
export ConnectionStrings__Auth='Host=<rds>;Port=5432;Database=vue_demo_auth;Username=<user>;Password=<password>'
export ConnectionStrings__Tasks='Host=<rds>;Port=5432;Database=vue_demo_tasks;Username=<user>;Password=<password>'
export ConnectionStrings__Orders='Host=<rds>;Port=5432;Database=vue_demo_orders;Username=<user>;Password=<password>'
export ConnectionStrings__Payments='Host=<rds>;Port=5432;Database=vue_demo_payments;Username=<user>;Password=<password>'
export ConnectionStrings__messaging='amqp://guest:guest@127.0.0.1:5672'

dotnet run --project backend.AppHost/backend.AppHost.csproj --launch-profile aws
```

For the current microservices AWS runtime, use:

```bash
./scripts/deploy.sh
```
