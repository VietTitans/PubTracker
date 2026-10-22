using DotNetEnv;
using RecordService.DataAccess.Summarization;
using RecordService.Models;

namespace test;

/// <summary>
/// Manual, opt-in verification that ChatCompletionsSummaryGenerator actually gets a summary
/// back from a real OpenAI-compatible endpoint. Skipped by default so it never runs as part of
/// `dotnet test`/CI (it makes a real, billed API call). To run it: remove the Skip attribute
/// below, run this test alone, then restore the Skip attribute before committing.
/// </summary>
public class ChatCompletionsSummaryGeneratorManualTest
{
    [Fact(Skip = "Manual only - hits a real OpenAI-compatible LLM endpoint. Remove Skip locally to run.")]
    public async Task SummarizeAsync_ReturnsSummaryFromRealEndpoint()
    {
        Env.Load(FindDockerEnvFile());

        var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL");
        var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY");
        var model = Environment.GetEnvironmentVariable("LLM_MODEL");

        Assert.False(string.IsNullOrWhiteSpace(baseUrl), "LLM_BASE_URL must be set in docker/.env to run this test.");
        Assert.False(string.IsNullOrWhiteSpace(apiKey), "LLM_API_KEY must be set in docker/.env to run this test.");
        Assert.False(string.IsNullOrWhiteSpace(model), "LLM_MODEL must be set in docker/.env to run this test.");

        var generator = new ChatCompletionsSummaryGenerator(new HttpClient(), baseUrl!, apiKey!, model!);

        var summary = await generator.SummarizeAsync(new List<LiteratureRecord>
        {
            new() { ExternalId = "manual:1", Title = "Effects of aerobic exercise on chronic low back pain", Abstract = "A randomized controlled trial found aerobic exercise reduced pain scores." },
            new() { ExternalId = "manual:2", Title = "Manual therapy for shoulder impingement syndrome", Abstract = "A systematic review of manual therapy interventions for shoulder impingement." }
        });

        Assert.False(string.IsNullOrWhiteSpace(summary));
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
