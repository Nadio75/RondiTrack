namespace RondiTrack.Tests.TestSupport;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Infrastructure;
using Testcontainers.PostgreSql;

public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17")
            .WithDatabase("ronditrack_test")
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

    public async Task SetMembershipRoleAsync(
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
        throw new InvalidOperationException(
            $"Membership not found for user {userId} " +
            $"in stokvel {stokvelId}.");
    }

    await db.StokvelMembers
        .Where(m =>
            m.StokvelId == stokvelId &&
            m.UserId == userId)
        .ExecuteUpdateAsync(update =>
            update.SetProperty(m => m.Role, role));
}
public async Task EnsureMembershipAsync(
    Guid stokvelId,
    Guid userId,
    string role = "Chairperson")
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
    else if (membership.Role != role)
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

        builder.ConfigureTestServices(services =>
        {
            // Replace JWT authentication only in this integration-test host.
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        TestAuthenticationHandler.SchemeName;

                    options.DefaultChallengeScheme =
                        TestAuthenticationHandler.SchemeName;

                    options.DefaultForbidScheme =
                        TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<
                    AuthenticationSchemeOptions,
                    TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Postgres collection")]
public class PostgresCollection
    : ICollectionFixture<PostgresApiFactory>
{
}