FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/backend.Functions/backend.Functions.csproj backend/backend.Functions/
COPY backend/backend.Domain/backend.Domain.csproj backend/backend.Domain/
COPY backend/backend.Infrastructure/backend.Infrastructure.csproj backend/backend.Infrastructure/
COPY backend/backend.Shared/backend.Shared.csproj backend/backend.Shared/
COPY backend/backend.Tasks/backend.Tasks.csproj backend/backend.Tasks/
COPY backend/backend.Orders/backend.Orders.csproj backend/backend.Orders/

RUN dotnet restore backend/backend.Functions/backend.Functions.csproj

COPY backend/ backend/

RUN dotnet publish backend/backend.Functions/backend.Functions.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4 AS runtime
WORKDIR /home/site/wwwroot

ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080
