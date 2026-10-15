using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RecordService.BusinessLogic.DigestOutboxService;
using RecordService.DataAccess;
using RecordService.DataAccess.Email;
using RecordService.DataAccess.ExternalSources;
using RecordService.DataAccess.Summarization;
using Npgsql;
using Testcontainers.PostgreSql;

namespace test;

/// <summary>
/// Boots the real RecordService app against its own throwaway database inside a shared
/// Testcontainers Postgres instance (schema applied by the app's own EF Core migrations, same as any other environment; see
/// Program.cs) and swaps the real PubMed/PEDro providers for FakeLiteratureSourceProvider, so
/// tests exercise the real HTTP -> Service -> DataAccess -> Postgres path without depending on
/// live external sources.
///
/// The connection string and ASPNETCORE_ENVIRONMENT are set via environment variables (not
/// ConfigureWebHost's ConfigureAppConfiguration) because Program.cs reads
/// builder.Configuration.GetConnectionString(...) before builder.Build() runs; environment
/// variables are picked up synchronously at WebApplication.CreateBuilder(args) time, so they're
/// guaranteed to be visible by then, matching how docker-compose already configures this app
/// (ConnectionStrings__DefaultConnection).
/// </summary>
public class PubTrackerWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // One container for the whole test run; each factory gets its own database inside it. Safe
    // because every factory-using class shares one xunit [Collection], so factories never overlap.
    // Testcontainers' Ryuk reaper removes the container when the test process exits.
    private static readonly Lazy<Task<PostgreSqlContainer>> SharedPostgres = new(async () =>
    {
        var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("pubtracker_test")
            .WithUsername("pubtracker")
            .WithPassword("pubtracker")
            .Build();
        await container.StartAsync();
        return container;
    });

    private readonly string _databaseName = $"pt_{Guid.NewGuid():N}";
    private string _adminConnectionString = null!;

    public readonly FakeEmailSender EmailSender = new();
    public readonly FakeSummaryGenerator SummaryGenerator = new();
    public readonly FakeLiteratureSourceProvider LiteratureSourceProvider = new();

    public async Task InitializeAsync()
    {
        var postgres = await SharedPostgres.Value;
        _adminConnectionString = postgres.GetConnectionString();

        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {_databaseName}", admin);
            await create.ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName }.ConnectionString;

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
        Environment.SetEnvironmentVariable("Email__ApiKey", "test-api-key");
        Environment.SetEnvironmentVariable("Email__FromAddress", "digest@example.com");
        Environment.SetEnvironmentVariable("Email__FromName", "PubTracker");
        // Tests drain the digest outbox explicitly (DrainOutboxAsync); keep the background worker from racing them.
        Environment.SetEnvironmentVariable("Scheduler__DigestOutboxIntervalSeconds", "86400");
        // Likewise park the automatic post-subscribe poll; tests poll explicitly.
        Environment.SetEnvironmentVariable("Scheduler__InitialPollDelaySeconds", "86400");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<LiteratureSourceFactory>();
            services.AddSingleton(new LiteratureSourceFactory(
                new ILiteratureSourceProvider[] { LiteratureSourceProvider, new FakePedroLiteratureSourceProvider(), new FakeUniqueLiteratureSourceProvider() }));

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.RemoveAll<ISummaryGenerator>();
            services.AddSingleton<ISummaryGenerator>(SummaryGenerator);
        });
    }

    /// <summary>
    /// A query's first poll is a baseline that never emails (see RecordPollingService.FetchOneAsync).
    /// Call after subscribing so the next poll is an ordinary one that does.
    /// </summary>
    public async Task SkipBaselinePollAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PubTrackerDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("UPDATE search_queries SET last_polled_at = TIMESTAMPTZ 'epoch' WHERE last_polled_at IS NULL");
    }

    /// <summary>Sends every due digest outbox row now (what DigestOutboxBackgroundService does on its interval).</summary>
    public async Task<int> DrainOutboxAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDigestOutboxService>().ProcessDueAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
