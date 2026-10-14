
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure;

namespace RondiTrack.Services;

public sealed class CycleClosingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CycleClosingBackgroundService> _logger;

    public CycleClosingBackgroundService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        IConfiguration configuration,
        ILogger<CycleClosingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        int intervalSeconds = _configuration.GetValue(
            "CycleClosing:IntervalSeconds", 3600);

        if (intervalSeconds < 60)
        {
            throw new InvalidOperationException(
                "CycleClosing:IntervalSeconds must be at least 60.");
        }

        _logger.LogInformation(
            "Cycle closing scheduler started. Interval: {IntervalSeconds} seconds",
            intervalSeconds);

        // Run immediately on startup, then at the configured interval.
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(intervalSeconds),
            _clock);

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
        List<Guid> stokvelIds;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            stokvelIds = await db.Stokvels
                .AsNoTracking()
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);
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
                "Unable to load stokvels for scheduled cycle closing.");

            return;
        }

        // Each stokvel gets a separate DbContext and service scope.
        // A failure for one stokvel will not stop the others.
        await Task.WhenAll(stokvelIds.Select(async stokvelId =>
        {
            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var closingService = scope.ServiceProvider
                    .GetRequiredService<CycleClosingService>();

                int closed = await closingService.CloseDueCyclesAsync(
                    stokvelId,
                    cancellationToken);

                if (closed > 0)
                {
                    _logger.LogInformation(
                        "Closed {CycleCount} cycles for stokvel {StokvelId}",
                        closed,
                        stokvelId);
                }
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
                    "Cycle closing failed for stokvel {StokvelId}. Will retry on the next run.",
                    stokvelId);
            }
        }));
    }
}
