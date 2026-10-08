namespace RondiTrack.Tests.TestSupport;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
    .WithDatabase("ronditrack_test")
    .WithUsername("postgres")
    .WithPassword("postgres")
    .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            _container.GetConnectionString());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Postgres collection")]
public class PostgresCollection : ICollectionFixture<PostgresApiFactory>
{
}