FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NotifyHub.slnx ./
COPY src/NotifyHub.Api/NotifyHub.Api.csproj src/NotifyHub.Api/
COPY src/NotifyHub.Contracts/NotifyHub.Contracts.csproj src/NotifyHub.Contracts/
COPY src/NotifyHub.Core/NotifyHub.Core.csproj src/NotifyHub.Core/
COPY src/NotifyHub.Infrastructure/NotifyHub.Infrastructure.csproj src/NotifyHub.Infrastructure/
RUN dotnet restore src/NotifyHub.Api/NotifyHub.Api.csproj

COPY src/NotifyHub.Api/ src/NotifyHub.Api/
COPY src/NotifyHub.Contracts/ src/NotifyHub.Contracts/
COPY src/NotifyHub.Core/ src/NotifyHub.Core/
COPY src/NotifyHub.Infrastructure/ src/NotifyHub.Infrastructure/
RUN dotnet publish src/NotifyHub.Api/NotifyHub.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotifyHub.Api.dll"]
