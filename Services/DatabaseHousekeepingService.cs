
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure;

namespace RondiTrack.Services;

/// <summary>
/// Deletes refresh tokens that have been expired
/// beyond the configured retention period.
/// </summary>
public sealed class DatabaseHousekeepingService
{
    private readonly RondiTrackDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseHousekeepingService> _logger;

    public DatabaseHousekeepingService(
        RondiTrackDbContext db,
        TimeProvider clock,
        IConfiguration configuration,
        ILogger<DatabaseHousekeepingService> logger)
    {
        _db = db;
        _clock = clock;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<int> CleanupExpiredRefreshTokensAsync(
        CancellationToken cancellationToken = default)
    {
        int retentionDays = _configuration.GetValue(
            "Housekeeping:RefreshTokenRetentionDays", 30);

        if (retentionDays < 1)
        {
            throw new InvalidOperationException(
                "Refresh token retention must be at least one day.");
        }

        DateTime cutoffUtc = _clock.GetUtcNow()
            .UtcDateTime.AddDays(-retentionDays);

        int deleted = await _db.RefreshTokens
            .Where(token => token.ExpiresAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation(
            "Refresh token housekeeping removed {DeletedCount} old tokens.",
            deleted);

        return deleted;
    }
}
