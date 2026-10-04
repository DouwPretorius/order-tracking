using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace stock_api.Tests;

public sealed class OrderApiFactory : WebApplicationFactory<Program>
{
    private readonly string connectionString;
    private readonly string baseConnectionString;
    private readonly string? previousConnectionString;
    private readonly string? previousDuplicateWindow;
    private readonly string schemaName = $"order_api_tests_{Guid.NewGuid():N}";

    public OrderApiFactory()
    {
        baseConnectionString = Environment.GetEnvironmentVariable("ORDER_API_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Set ORDER_API_TEST_CONNECTION_STRING to a PostgreSQL connection string for the API integration tests.");
        connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schemaName,
        }.ConnectionString;

        using var connection = new NpgsqlConnection(baseConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
        command.ExecuteNonQuery();

        previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OrdersDatabase");
        previousDuplicateWindow = Environment.GetEnvironmentVariable("OrderSubmission__DuplicateWindowSeconds");
        Environment.SetEnvironmentVariable("ConnectionStrings__OrdersDatabase", connectionString);
        Environment.SetEnvironmentVariable("OrderSubmission__DuplicateWindowSeconds", "120");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                using var connection = new NpgsqlConnection(baseConnectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
                command.ExecuteNonQuery();
            }
            finally
            {
                Environment.SetEnvironmentVariable("ConnectionStrings__OrdersDatabase", previousConnectionString);
                Environment.SetEnvironmentVariable("OrderSubmission__DuplicateWindowSeconds", previousDuplicateWindow);
            }
        }
    }
}
