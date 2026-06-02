FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/backend.Auth.Api/backend.Auth.Api.csproj backend/backend.Auth.Api/
COPY backend/backend.Domain/backend.Domain.csproj backend/backend.Domain/
COPY backend/backend.Infrastructure/backend.Infrastructure.csproj backend/backend.Infrastructure/
COPY backend/backend.ServiceDefaults/backend.ServiceDefaults.csproj backend/backend.ServiceDefaults/
COPY backend/backend.Shared/backend.Shared.csproj backend/backend.Shared/

RUN dotnet restore backend/backend.Auth.Api/backend.Auth.Api.csproj

COPY backend/ backend/

RUN dotnet publish backend/backend.Auth.Api/backend.Auth.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "backend.Auth.Api.dll"]
