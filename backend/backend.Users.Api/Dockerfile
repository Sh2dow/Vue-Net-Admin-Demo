FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/backend.Users.Api/backend.Users.Api.csproj backend/backend.Users.Api/
COPY backend/backend.Users/backend.Users.csproj backend/backend.Users/
COPY backend/backend.Domain/backend.Domain.csproj backend/backend.Domain/
COPY backend/backend.Infrastructure/backend.Infrastructure.csproj backend/backend.Infrastructure/
COPY backend/backend.ServiceDefaults/backend.ServiceDefaults.csproj backend/backend.ServiceDefaults/
COPY backend/backend.Shared/backend.Shared.csproj backend/backend.Shared/

RUN dotnet restore backend/backend.Users.Api/backend.Users.Api.csproj

COPY backend/ backend/

RUN dotnet publish backend/backend.Users.Api/backend.Users.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "backend.Users.Api.dll"]
