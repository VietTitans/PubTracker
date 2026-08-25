using DotNetEnv;
using RecordService.BusinessLogic.DigestService;
using RecordService.DataAccess.Email;
using RecordService.DataAccess.ExternalSources;

namespace test;

/// <summary>
/// Manual, opt-in verification that PedroProvider's Playwright scraping still matches PEDro's
/// live site structure. Skipped by default (real network + headless browser, not for CI). To
/// run it: remove the Skip attribute below, run this test alone, then restore Skip before
/// committing.
/// </summary>
public class PedroProviderManualTest
{
    private const string TestUrl =
        "https://search.pedro.org.au/advanced-search/results?abstract_with_title=&therapy=VL01387&problem=VL01371&body_part=VL01396&subdiscipline=VL01359&topic=VL01402&method=0&authors_association=&title=&source=&year_of_publication=2020&date_record_was_created=&nscore=&perpage=20&lop=and&find=&find=Start+Search";

    [Fact]
    //[Fact(Skip = "Manual only - launches a real headless browser against the live PEDro site and sends a real email. Remove Skip locally to run.")]
    public async Task SearchAsync_ParsesRealPedroResultsPage()
    {
        await using var provider = new PedroProvider();

        var result = await provider.SearchAsync(TestUrl);

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.NotEmpty(result.NewRecords);

        foreach (var record in result.NewRecords.Take(5))
        {
            Console.WriteLine($"{record.Doi} | {record.Title} | {record.SourceUrl}");
        }

        Env.Load(FindDockerEnvFile());

        var apiKey = Environment.GetEnvironmentVariable("EMAIL_API_KEY");
        var fromAddress = Environment.GetEnvironmentVariable("EMAIL_FROM_ADDRESS");
        var fromName = Environment.GetEnvironmentVariable("EMAIL_FROM_NAME") ?? "PubTracker";

        Assert.False(string.IsNullOrWhiteSpace(apiKey), "EMAIL_API_KEY must be set in docker/.env to run this test.");
        Assert.False(string.IsNullOrWhiteSpace(fromAddress), "EMAIL_FROM_ADDRESS must be set in docker/.env to run this test.");

        var subject = $"{result.NewRecords.Count} new record{(result.NewRecords.Count == 1 ? "" : "s")} for your search";
        var htmlBody = DigestService.BuildHtmlBody(TestUrl, result.NewRecords);

        var sender = new BrevoEmailSender(new HttpClient(), apiKey!, fromAddress!, fromName);
        await sender.SendAsync(fromAddress!, subject, htmlBody);
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
