cd "d:\Repos\Interview\Keycloak-UI-Demo\backend"
dotnet ef database update --project backend.Domain\backend.Domain.csproj --startup-project backend.Payments.Api\backend.Payments.Api.csproj --context PaymentsDbContext --force
