FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/backend.Api/backend.Api.csproj backend/backend.Api/
COPY backend/backend.Domain/backend.Domain.csproj backend/backend.Domain/
COPY backend/backend.Infrastructure/backend.Infrastructure.csproj backend/backend.Infrastructure/
COPY backend/backend.Orders/backend.Orders.csproj backend/backend.Orders/
COPY backend/backend.Payments/backend.Payments.csproj backend/backend.Payments/
COPY backend/backend.ServiceDefaults/backend.ServiceDefaults.csproj backend/backend.ServiceDefaults/
COPY backend/backend.Shared/backend.Shared.csproj backend/backend.Shared/
COPY backend/backend.Tasks/backend.Tasks.csproj backend/backend.Tasks/
COPY backend/backend.Users/backend.Users.csproj backend/backend.Users/

RUN dotnet restore backend/backend.Api/backend.Api.csproj

COPY backend/ backend/

RUN dotnet publish backend/backend.Api/backend.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "backend.Api.dll"]
