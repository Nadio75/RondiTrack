
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace RondiTrack.Health;

public sealed class PostgresReadinessHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public PostgresReadinessHealthCheck(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString =
            _configuration.GetConnectionString("RondiTrack");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy(
                "Database connection is not configured.");
        }

        try
        {
            // Use a short connection timeout specifically
            // for health checks, not normal application queries.
            var connectionSettings =
                new NpgsqlConnectionStringBuilder(connectionString)
                {
                    Timeout = 3,
                    Pooling = false
                };

            await using var connection =
                new NpgsqlConnection(
                    connectionSettings.ConnectionString);

            await connection.OpenAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
            when (exception is NpgsqlException
                or TimeoutException
                or OperationCanceledException
                or InvalidOperationException)
        {
            // The endpoint never exposes this exception.
            return HealthCheckResult.Unhealthy(
                "PostgreSQL is unavailable.");
        }
    }
}

