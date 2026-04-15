using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace NotifyHub.Tests.Integration.Fixtures;

/// <summary>
/// Integration test factory that connects to infrastructure services
/// started via docker compose (postgres + rabbitmq).
/// Run `docker compose up postgres rabbitmq -d` before running integration tests.
/// </summary>
public class NotifyHubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string AdminConnection =
        "Host=localhost;Port=5432;Database=postgres;Username=notifyhub;Password=notifyhub";

    private const string TestDbConnection =
        "Host=localhost;Port=5432;Database=notifyhub_test;Username=notifyhub;Password=notifyhub";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", TestDbConnection);
        builder.UseSetting("RabbitMQ:Host", "localhost");
        builder.UseSetting("RabbitMQ:Port", "5672");
        builder.UseSetting("RabbitMQ:Username", "notifyhub");
        builder.UseSetting("RabbitMQ:Password", "notifyhub");
        builder.UseEnvironment("Development");
    }

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(AdminConnection);
        await conn.OpenAsync();

        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = 'notifyhub_test'";
        var exists = await checkCmd.ExecuteScalarAsync();

        if (exists is null)
        {
            await using var createCmd = conn.CreateCommand();
            createCmd.CommandText = "CREATE DATABASE notifyhub_test";
            await createCmd.ExecuteNonQueryAsync();
        }
    }

    public new Task DisposeAsync() => Task.CompletedTask;
}
