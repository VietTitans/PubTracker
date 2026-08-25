using DotNetEnv;
using RecordService.DataAccess.Email;

namespace test;

/// <summary>
/// Manual, opt-in verification that BrevoEmailSender actually delivers through the real Brevo
/// API. Skipped by default so it never runs as part of `dotnet test`/CI (it sends a real email
/// and requires a live API key). To run it: remove the Skip attribute below, run this test alone,
/// then restore the Skip attribute before committing.
/// </summary>
public class BrevoEmailSenderManualTest
{
    [Fact(Skip = "Manual only - hits the real Brevo API and sends a real email. Remove Skip locally to run.")]
    public async Task SendAsync_DeliversRealEmailViaBrevo()
    {
        Env.Load(FindDockerEnvFile());

        var apiKey = Environment.GetEnvironmentVariable("EMAIL_API_KEY");
        var fromAddress = Environment.GetEnvironmentVariable("EMAIL_FROM_ADDRESS");
        var fromName = Environment.GetEnvironmentVariable("EMAIL_FROM_NAME") ?? "PubTracker";

        Assert.False(string.IsNullOrWhiteSpace(apiKey), "EMAIL_API_KEY must be set in docker/.env to run this test.");
        Assert.False(string.IsNullOrWhiteSpace(fromAddress), "EMAIL_FROM_ADDRESS must be set in docker/.env to run this test.");

        var sender = new BrevoEmailSender(new HttpClient(), apiKey!, fromAddress!, fromName);

        await sender.SendAsync(fromAddress!, "PubTracker manual test", "<p>If you're reading this, BrevoEmailSender works.</p>");
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
