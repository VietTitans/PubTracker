using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using RecordService.DataAccess.Chat;
using RecordService.DataAccess.Email;
using RecordService.DataAccess.Embeddings;
using RecordService.DataAccess.ExternalSources;
using Testcontainers.PostgreSql;

namespace test;

/// <summary>
/// Boots the real RecordService app against an ephemeral Testcontainers Postgres instance
/// (schema applied from database/schema/init.sql) and swaps the real PubMed/PEDro providers
/// for FakeLiteratureSourceProvider, so tests exercise the real HTTP -> Service -> DataAccess
/// -> Postgres path without depending on live external sources.
///
/// The connection string and ASPNETCORE_ENVIRONMENT are set via environment variables (not
/// ConfigureWebHost's ConfigureAppConfiguration) because Program.cs reads
/// builder.Configuration.GetConnectionString(...) before builder.Build() runs - environment
/// variables are picked up synchronously at WebApplication.CreateBuilder(args) time, so they're
/// guaranteed to be visible by then, matching how docker-compose already configures this app
/// (ConnectionStrings__DefaultConnection).
/// </summary>
public class PubTrackerWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("pubtracker_test")
        .WithUsername("pubtracker")
        .WithPassword("pubtracker")
        .Build();

    public readonly FakeEmailSender EmailSender = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Email__ApiKey", "test-api-key");
        Environment.SetEnvironmentVariable("Email__FromAddress", "digest@example.com");
        Environment.SetEnvironmentVariable("Email__FromName", "PubTracker");
        Environment.SetEnvironmentVariable("OpenAi__ApiKey", "test-api-key");
        Environment.SetEnvironmentVariable("Anthropic__ApiKey", "test-api-key");

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema", "init.sql");
        var schemaSql = await File.ReadAllTextAsync(schemaPath);

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(schemaSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<LiteratureSourceFactory>();
            services.AddSingleton(new LiteratureSourceFactory(
                new ILiteratureSourceProvider[] { new FakeLiteratureSourceProvider(), new FakePedroLiteratureSourceProvider() }));

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.RemoveAll<IEmbeddingClient>();
            services.AddSingleton<IEmbeddingClient>(new FakeEmbeddingClient());

            services.RemoveAll<IChatCompletionClient>();
            services.AddSingleton<IChatCompletionClient>(new FakeChatCompletionClient());
        });
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
