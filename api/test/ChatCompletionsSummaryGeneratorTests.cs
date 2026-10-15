using System.Net;
using System.Text;
using System.Text.Json;
using RecordService.DataAccess.Summarization;
using RecordService.Models;

namespace test;

/// <summary>
/// Covers how ChatCompletionsSummaryGenerator.SummarizeAsync (the digest summary) handles batch
/// size: one call when the batch fits one chunk, chunk calls plus one combining call when it does
/// not (so every record is covered), and only records that have an abstract are sent.
/// </summary>
public class ChatCompletionsSummaryGeneratorTests
{
    private class RecordingHandler : HttpMessageHandler
    {
        public List<(string System, string User)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync(cancellationToken).Result);
            var messages = body.RootElement.GetProperty("messages");
            lock (Requests)
            {
                Requests.Add((messages[0].GetProperty("content").GetString()!, messages[1].GetProperty("content").GetString()!));
                var reply = $"reply-{Requests.Count}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = reply } } } }),
                        Encoding.UTF8, "application/json")
                });
            }
        }
    }

    private static (ChatCompletionsSummaryGenerator Generator, RecordingHandler Handler) Create(int chunkSize = 40, int abstractChars = 1000)
    {
        var handler = new RecordingHandler();
        var generator = new ChatCompletionsSummaryGenerator(
            new HttpClient(handler), "https://llm.test/v1/", "key", "model", chunkSize, abstractChars);
        return (generator, handler);
    }

    private static List<LiteratureRecord> Records(int count, string? abstractText = "An abstract.") =>
        Enumerable.Range(1, count).Select(i => new LiteratureRecord
        {
            ExternalId = $"r:{i}",
            Title = $"Title {i}",
            Abstract = abstractText
        }).ToList();

    [Fact]
    public async Task BatchThatFitsOneChunk_UsesOneCall_WithAllRecordsAndTheFiveToSixSentencePrompt()
    {
        var (generator, handler) = Create(chunkSize: 40);

        var summary = await generator.SummarizeAsync(Records(3));

        var request = Assert.Single(handler.Requests);
        Assert.Equal("reply-1", summary);
        Assert.Contains("5-6 sentences", request.System);
        Assert.All(new[] { "Title 1", "Title 2", "Title 3" }, title => Assert.Contains(title, request.User));
    }

    [Fact]
    public async Task BatchLargerThanOneChunk_SummarizesEveryChunk_ThenCombines()
    {
        var (generator, handler) = Create(chunkSize: 2);

        var summary = await generator.SummarizeAsync(Records(5)); // chunks of 2, 2, 1

        Assert.Equal(4, handler.Requests.Count); // 3 chunk calls + 1 combining call
        var chunkCalls = handler.Requests.Where(r => r.System.Contains("3-4 sentences")).ToList();
        Assert.Equal(3, chunkCalls.Count);
        foreach (var title in Enumerable.Range(1, 5).Select(i => $"Title {i}"))
        {
            Assert.Single(chunkCalls, r => r.User.Contains(title));
        }

        var combine = Assert.Single(handler.Requests, r => r.System.Contains("5-6 sentences"));
        Assert.Equal(combine.System, handler.Requests.Last().System); // combining call goes last
        Assert.Equal("reply-4", summary);
        Assert.Equal(3, combine.User.Split('\n').Count(line => line.StartsWith("- reply-")));
    }

    [Fact]
    public async Task RecordsWithoutAbstract_AreNotSent_AndNoCallIsMadeWhenNoneHasOne()
    {
        var (generator, handler) = Create();
        var records = Records(2);
        records.Add(new LiteratureRecord { ExternalId = "r:none", Title = "No abstract title", Abstract = null });

        await generator.SummarizeAsync(records);

        Assert.DoesNotContain("No abstract title", Assert.Single(handler.Requests).User);

        var (emptyGenerator, emptyHandler) = Create();
        var summary = await emptyGenerator.SummarizeAsync(Records(3, abstractText: null));
        Assert.Equal(string.Empty, summary);
        Assert.Empty(emptyHandler.Requests);
    }

    [Fact]
    public async Task LongAbstract_IsCutToConfiguredLength()
    {
        var (generator, handler) = Create(abstractChars: 10);

        await generator.SummarizeAsync(Records(1, abstractText: new string('x', 50)));

        var user = Assert.Single(handler.Requests).User;
        Assert.Contains(new string('x', 10) + "...", user);
        Assert.DoesNotContain(new string('x', 11), user);
    }
}
