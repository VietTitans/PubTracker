using DotNetEnv;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector.Npgsql;
using RecordService.BusinessLogic.DigestService;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DataAccess.Chat;
using RecordService.DataAccess.Email;
using RecordService.DataAccess.Embeddings;
using RecordService.DataAccess.ExternalSources;

namespace test;

/// <summary>
/// Manual, opt-in verification of the full production digest path against your real dev
/// database and real subscriptions: polls the search queries listed in SearchQueryIds below,
/// fetches live results from each source, persists them, and sends one combined digest email
/// per real subscriber - not one email per query. A subscriber who also has other queries
/// pending outside this list gets those swept into the same email too (see
/// RecordPollingService.PollSearchQueriesAsync's sweep behavior), so a real subscriber may
/// receive mail covering queries beyond just the ones listed here.
///
/// Before polling, it resets each listed query's per-subscriber digest watermarks (and pushes
/// last_polled_at back to 2010) so every run re-sends a fresh digest of whatever is already
/// linked, instead of only picking up records newly discovered since the last run - that's
/// what makes it repeatable without manual SQL between runs. Skipped by default (real network,
/// real DB, real email - not for CI). To run it: fill in SearchQueryIds with real
/// search_queries.id values, remove the Skip attribute, run this test alone, then restore
/// Skip and clear the list before committing.
/// </summary>
public class DigestPollEndToEndManualTest
{
    private static readonly int[] SearchQueryIds = { 12, 23, 1, 2 }; // set to the real search_queries.id values you want to poll

    [Fact(Skip = "Manual only - hits live sources, your real dev DB, and sends real emails via Brevo. Remove Skip locally to run.")]
    public async Task PollSearchQueries_SendsRealDigestEmailsToRealSubscribers()
    {
        Assert.NotEmpty(SearchQueryIds);

        Env.Load(FindDockerEnvFile());

        var connectionString = BuildConnectionString();
        var emailApiKey = Environment.GetEnvironmentVariable("EMAIL_API_KEY");
        var emailFromAddress = Environment.GetEnvironmentVariable("EMAIL_FROM_ADDRESS");
        var emailFromName = Environment.GetEnvironmentVariable("EMAIL_FROM_NAME") ?? "PubTracker";
        var ncbiApiKey = Environment.GetEnvironmentVariable("NCBI_API_KEY");
        var ncbiContactEmail = Environment.GetEnvironmentVariable("NCBI_CONTACT_EMAIL");
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        Assert.False(string.IsNullOrWhiteSpace(emailApiKey), "EMAIL_API_KEY must be set in docker/.env to run this test.");
        Assert.False(string.IsNullOrWhiteSpace(emailFromAddress), "EMAIL_FROM_ADDRESS must be set in docker/.env to run this test.");
        Assert.False(string.IsNullOrWhiteSpace(openAiApiKey), "OPENAI_API_KEY must be set in docker/.env to run this test.");

        var sourceFactory = new LiteratureSourceFactory(new ILiteratureSourceProvider[]
        {
            new PubMedProvider(new HttpClient(), ncbiApiKey, ncbiContactEmail),
            new PedroProvider()
        });

        await ResetDigestWatermarksAsync(connectionString, SearchQueryIds);

        var vectorDataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
#pragma warning disable NPG9001 // see Program.cs's registration for why this is suppressed
        vectorDataSourceBuilder.AddTypeInfoResolverFactory(new VectorTypeInfoResolverFactory());
#pragma warning restore NPG9001
        var embeddingClient = new OpenAiEmbeddingClient(new HttpClient(), openAiApiKey!, NullLogger<OpenAiEmbeddingClient>.Instance);

        var searchQueriesDataAccess = new SearchQueriesDataAccess(connectionString, sourceFactory);
        var recordsDataAccess = new RecordsDataAccess(vectorDataSourceBuilder.Build(), embeddingClient);
        var usersDataAccess = new UsersDataAccess(connectionString, new HttpContextAccessor());
        var emailSender = new BrevoEmailSender(new HttpClient(), emailApiKey!, emailFromAddress!, emailFromName);
        var digestService = new DigestService(usersDataAccess, emailSender, NullLogger<DigestService>.Instance);
        var pollingService = new RecordPollingService(searchQueriesDataAccess, recordsDataAccess, digestService, NullLogger<RecordPollingService>.Instance);

        var results = await pollingService.PollSearchQueriesAsync(SearchQueryIds);

        foreach (var result in results)
        {
            Console.WriteLine(result.IsSuccessful
                ? $"Search query {result.SearchQueryId}: OK, {result.NewRecordCount} new record(s)."
                : $"Search query {result.SearchQueryId}: FAILED - {result.ErrorMessage}");
        }

        Assert.All(results, r => Assert.True(r.IsSuccessful, $"Search query {r.SearchQueryId}: {r.ErrorMessage}"));
    }

    // Deletes each listed query's per-subscriber digest watermark rows (so every subscriber is
    // treated as "never sent" and this run re-sends everything already linked) and pushes
    // last_polled_at back to 2010 (so PedroProvider takes its incremental, record-fetching
    // path instead of another baseline-only call) - lets this test be re-run repeatedly
    // without manual SQL between runs (see class doc comment).
    private static async Task ResetDigestWatermarksAsync(string connectionString, int[] searchQueryIds)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        using (var deleteWatermarksCommand = new NpgsqlCommand(
            "DELETE FROM user_search_query_digests WHERE search_query_id = ANY(@ids)", connection))
        {
            deleteWatermarksCommand.Parameters.AddWithValue("@ids", searchQueryIds);
            await deleteWatermarksCommand.ExecuteNonQueryAsync();
        }

        using (var resetPolledAtCommand = new NpgsqlCommand(
            "UPDATE search_queries SET last_polled_at = '2010-01-01' WHERE id = ANY(@ids)", connection))
        {
            resetPolledAtCommand.Parameters.AddWithValue("@ids", searchQueryIds);
            await resetPolledAtCommand.ExecuteNonQueryAsync();
        }
    }

    private static string BuildConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var db = Environment.GetEnvironmentVariable("POSTGRES_DB");
        var user = Environment.GetEnvironmentVariable("POSTGRES_USER");
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

        return $"Host={host};Port={port};Database={db};Username={user};Password={password}";
    }

    private static string FindDockerEnvFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "docker", ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate docker/.env by walking up from the test output directory.");
    }
}
