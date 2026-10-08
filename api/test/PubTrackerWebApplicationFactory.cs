using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RecordService.BusinessLogic.DigestOutboxService;
using RecordService.DataAccess.Email;
using RecordService.DataAccess.ExternalSources;
using RecordService.DataAccess.Summarization;
using Testcontainers.PostgreSql;

namespace test;

/// <summary>
/// Boots the real RecordService app against an ephemeral Testcontainers Postgres instance
/// (schema applied by the app's own EF Core migrations, same as any other environment; see
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
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("pubtracker_test")
        .WithUsername("pubtracker")
        .WithPassword("pubtracker")
        .Build();

    public readonly FakeEmailSender EmailSender = new();
    public readonly FakeSummaryGenerator SummaryGenerator = new();
    public readonly FakeLiteratureSourceProvider LiteratureSourceProvider = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Email__ApiKey", "test-api-key");
        Environment.SetEnvironmentVariable("Email__FromAddress", "digest@example.com");
        Environment.SetEnvironmentVariable("Email__FromName", "PubTracker");
        // Tests drain the digest outbox explicitly (DrainOutboxAsync); keep the background worker from racing them.
        Environment.SetEnvironmentVariable("Scheduler__DigestOutboxIntervalSeconds", "86400");
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

    /// <summary>Sends every due digest outbox row now (what DigestOutboxBackgroundService does on its interval).</summary>
    public async Task<int> DrainOutboxAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDigestOutboxService>().ProcessDueAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
