
namespace RondiTrack.Services;

public sealed class DatabaseHousekeepingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseHousekeepingBackgroundService> _logger;

    public DatabaseHousekeepingBackgroundService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        IConfiguration configuration,
        ILogger<DatabaseHousekeepingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        int intervalHours = _configuration.GetValue(
            "Housekeeping:IntervalHours", 24);

        if (intervalHours < 1 || intervalHours > 168)
        {
            throw new InvalidOperationException(
                "Housekeeping interval must be between 1 and 168 hours.");
        }

        _logger.LogInformation(
            "Database housekeeping scheduler started. Interval: {IntervalHours} hours",
            intervalHours);

        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(
            TimeSpan.FromHours(intervalHours), _clock);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }

    public async Task RunOnceAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var housekeeping = scope.ServiceProvider
                .GetRequiredService<DatabaseHousekeepingService>();

            int deleted = await housekeeping
                .CleanupExpiredRefreshTokensAsync(cancellationToken);

            _logger.LogInformation(
                "Housekeeping completed. Removed {DeletedCount} expired refresh tokens.",
                deleted);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Database housekeeping failed. Will retry on the next scheduled run.");
        }
    }
}
