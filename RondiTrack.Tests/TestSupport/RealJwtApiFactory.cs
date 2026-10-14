namespace RondiTrack.Tests.TestSupport;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Infrastructure;
using Testcontainers.PostgreSql;

public sealed class RealJwtApiFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17")
            .WithDatabase("ronditrack_security_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        await db.Database.MigrateAsync();
    }

    public async Task EnsureMembershipAsync(
        Guid stokvelId,
        Guid userId,
        string role)
    {
        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var membership = await db.StokvelMembers
            .SingleOrDefaultAsync(m =>
                m.StokvelId == stokvelId &&
                m.UserId == userId);

        if (membership is null)
        {
            db.StokvelMembers.Add(
                new RondiTrack.Domain.StokvelMember(
                    stokvelId,
                    userId,
                    role));

            await db.SaveChangesAsync();
        }
        else
        {
            await db.StokvelMembers
                .Where(m =>
                    m.StokvelId == stokvelId &&
                    m.UserId == userId)
                .ExecuteUpdateAsync(update =>
                    update.SetProperty(m => m.Role, role));
        }
    }


    protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.UseSetting(
        "ConnectionStrings:RondiTrack",
        _container.GetConnectionString());
    builder.UseSetting("CycleClosing:Enabled", "false");
builder.UseSetting("Housekeeping:Enabled", "false");


    builder.UseSetting("Jwt:Issuer", "RondiTrackSecurityTests");
    builder.UseSetting("Jwt:Audience", "RondiTrackSecurityTests");

    // Dedicated test-only signing key, not a production secret.
    builder.UseSetting(
        "Jwt:SigningKey",
        Convert.ToBase64String(new byte[32]));

    // The security tests perform many legitimate logins.
    // Use a high limit here so unrelated tests do not
    // interfere with each other.
    //
    // This setting applies only to this test server.
    builder.UseSetting(
        "RateLimiting:Login:PermitLimit",
        "1000");
}

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Real JWT security collection")]
public sealed class RealJwtSecurityCollection
    : ICollectionFixture<RealJwtApiFactory>
{
}