using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using RecordService.Models;

namespace RecordService.DataAccess.Summarization;

/// <summary>
/// Summarizes a digest's new records via the OpenAI-compatible chat-completions wire format
/// (POST {baseUrl}/chat/completions, response choices[0].message.content). OpenAI, Azure OpenAI,
/// Groq, local Ollama, and OpenRouter (which fronts Anthropic/Gemini through the same shape) all
/// speak this format, so swapping LLM providers is a config change (Llm:BaseUrl/Model/ApiKey),
/// not a new implementation. baseUrl must include the API version path and a trailing slash
/// (e.g. "https://api.openai.com/v1/") so it combines correctly with the relative request URI.
///
/// The digest summary covers every new record that has an abstract. A batch that fits in one
/// chunk (Llm:SummaryChunkSize) is summarized in a single call, which lets the model relate the
/// records to each other. A bigger batch is summarized chunk by chunk, and the chunk summaries
/// are then combined in one final call, so no record is dropped however large the batch is.
/// </summary>
public class ChatCompletionsSummaryGenerator : ISummaryGenerator
{
    private const int AbstractCharCap = 500; // author intention only; the digest summary uses summaryAbstractChars
    private const int MaxConcurrentChunkCalls = 5;

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly int _summaryChunkSize;
    private readonly int _summaryAbstractChars;

    public ChatCompletionsSummaryGenerator(
        HttpClient httpClient, string baseUrl, string apiKey, string model,
        int summaryChunkSize = 40, int summaryAbstractChars = 1000)
    {
        _summaryChunkSize = Math.Max(1, summaryChunkSize);
        _summaryAbstractChars = Math.Max(1, summaryAbstractChars);
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _model = model ?? throw new ArgumentNullException(nameof(model));

        _httpClient.BaseAddress = new Uri(baseUrl ?? throw new ArgumentNullException(nameof(baseUrl)));
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey ?? throw new ArgumentNullException(nameof(apiKey)));
    }

    public async Task<string> SummarizeAsync(IReadOnlyList<LiteratureRecord> records)
    {
        var withAbstract = records.Where(r => !string.IsNullOrWhiteSpace(r.Abstract)).ToList();
        if (withAbstract.Count == 0)
        {
            return string.Empty;
        }

        var chunks = withAbstract.Chunk(_summaryChunkSize).ToList();
        if (chunks.Count == 1)
        {
            return await RequestChatCompletionAsync(FinalSummaryPrompt, BuildRecordsPrompt(chunks[0]));
        }

        using var throttle = new SemaphoreSlim(MaxConcurrentChunkCalls);
        var partials = await Task.WhenAll(chunks.Select(async chunk =>
        {
            await throttle.WaitAsync();
            try
            {
                return await RequestChatCompletionAsync(ChunkSummaryPrompt, BuildRecordsPrompt(chunk));
            }
            finally
            {
                throttle.Release();
            }
        }));

        return await RequestChatCompletionAsync(
            FinalSummaryPrompt,
            "Summaries of consecutive batches of the new records, to combine into one summary:\n"
            + string.Join("\n", partials.Select(partial => "- " + partial)));
    }

    public async Task<string> GenerateAuthorIntentionAsync(LiteratureRecord record)
    {
        return await RequestChatCompletionAsync(
            "In one or two short sentences, state what the study's authors set out to investigate or show, based on its title and abstract. Do not restate the title verbatim. Reply with those sentences only - no title, heading, or markdown formatting. Avoid emdashes, bullet points, or lists. Do not include any text that is not part of the summary.",
            BuildRecordPrompt(record));
    }

    private async Task<string> RequestChatCompletionAsync(string systemPrompt, string userPrompt)
    {
        var response = await _httpClient.PostAsJsonAsync("chat/completions", new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        });

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Chat completions request failed ({(int)response.StatusCode}): {body}");
        }

        var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
        return completion?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? string.Empty;
    }

    private const string FinalSummaryPrompt =
        "You write a friendly summary (5-6 sentences) of what's new in this email update about research articles, based on their abstracts. Use plain, everyday language a non-technical reader would understand - avoid jargon. Reply with the summary prose only - no title, heading, or markdown formatting. Avoid emdashes, bullet points, or lists. Do not include any text that is not part of the summary.";

    private const string ChunkSummaryPrompt =
        "You summarize one batch of new research articles, based on their abstracts, in 3-4 sentences covering the main topics and findings. Use plain, everyday language. Reply with the summary prose only - no title, heading, or markdown formatting. Avoid emdashes, bullet points, or lists. Do not include any text that is not part of the summary.";

    private string BuildRecordsPrompt(IEnumerable<LiteratureRecord> records)
    {
        var sb = new StringBuilder("New records to summarize:\n");
        foreach (var record in records)
        {
            var abstractText = record.Abstract!.Length > _summaryAbstractChars
                ? record.Abstract[.._summaryAbstractChars] + "..."
                : record.Abstract;
            sb.Append("- ").Append(record.Title).Append(": ").Append(abstractText).Append('\n');
        }
        return sb.ToString();
    }

    private static string BuildRecordPrompt(LiteratureRecord record)
    {
        var sb = new StringBuilder("Title: ").Append(record.Title);
        if (!string.IsNullOrWhiteSpace(record.Abstract))
        {
            var truncated = record.Abstract.Length > AbstractCharCap
                ? record.Abstract[..AbstractCharCap] + "..."
                : record.Abstract;
            sb.Append("\nAbstract: ").Append(truncated);
        }
        return sb.ToString();
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }

        public class Choice
        {
            [JsonPropertyName("message")]
            public Message? Message { get; set; }
        }

        public class Message
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }
}
