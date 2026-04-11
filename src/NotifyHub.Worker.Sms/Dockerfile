FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NotifyHub.slnx ./
COPY src/NotifyHub.Worker.Sms/NotifyHub.Worker.Sms.csproj src/NotifyHub.Worker.Sms/
COPY src/NotifyHub.Contracts/NotifyHub.Contracts.csproj src/NotifyHub.Contracts/
COPY src/NotifyHub.Core/NotifyHub.Core.csproj src/NotifyHub.Core/
COPY src/NotifyHub.Infrastructure/NotifyHub.Infrastructure.csproj src/NotifyHub.Infrastructure/
RUN dotnet restore src/NotifyHub.Worker.Sms/NotifyHub.Worker.Sms.csproj

COPY src/NotifyHub.Worker.Sms/ src/NotifyHub.Worker.Sms/
COPY src/NotifyHub.Contracts/ src/NotifyHub.Contracts/
COPY src/NotifyHub.Core/ src/NotifyHub.Core/
COPY src/NotifyHub.Infrastructure/ src/NotifyHub.Infrastructure/
RUN dotnet publish src/NotifyHub.Worker.Sms/NotifyHub.Worker.Sms.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotifyHub.Worker.Sms.dll"]
