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
/// One call covers the whole digest rather than summarizing per-record/per-cluster first: even a
/// large digest's record titles/abstracts fit easily in a single prompt within any modern model's
/// context window, and one call lets the model relate records to each other instead of
/// re-synthesizing from isolated per-record summaries. RecordCap/AbstractCharCap are just a cheap
/// safety net for the unlikely oversized-digest case.
/// </summary>
public class ChatCompletionsSummaryGenerator : ISummaryGenerator
{
    private const int RecordCap = 50;
    private const int AbstractCharCap = 500;

    private readonly HttpClient _httpClient;
    private readonly string _model;

    public ChatCompletionsSummaryGenerator(HttpClient httpClient, string baseUrl, string apiKey, string model)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _model = model ?? throw new ArgumentNullException(nameof(model));

        _httpClient.BaseAddress = new Uri(baseUrl ?? throw new ArgumentNullException(nameof(baseUrl)));
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey ?? throw new ArgumentNullException(nameof(apiKey)));
    }

    public async Task<string> SummarizeAsync(IReadOnlyList<LiteratureRecord> records)
    {
        if (records.Count == 0)
        {
            return string.Empty;
        }

        return await RequestChatCompletionAsync(
            "You write a short, friendly summary (2-4 sentences) of what's new in this email update about research articles. Use plain, everyday language a non-technical reader would understand - avoid jargon. Reply with the summary prose only - no title, heading, or markdown formatting. Avoid emdashes, bullet points, or lists. Do not include any text that is not part of the summary.",
            BuildRecordsPrompt(records));
    }

    public async Task<string> GenerateAuthorIntentionAsync(LiteratureRecord record)
    {
        return await RequestChatCompletionAsync(
            "In one short sentence, state what the study's authors set out to investigate or show, based on its title and abstract. Do not restate the title verbatim. Reply with that one sentence only - no title, heading, or markdown formatting. Avoid emdashes, bullet points, or lists. Do not include any text that is not part of the summary.",
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

    private static string BuildRecordsPrompt(IReadOnlyList<LiteratureRecord> records)
    {
        var sb = new StringBuilder("New records to summarize:\n");
        foreach (var record in records.Take(RecordCap))
        {
            sb.Append("- ").Append(record.Title);
            if (!string.IsNullOrWhiteSpace(record.Abstract))
            {
                var truncated = record.Abstract.Length > AbstractCharCap
                    ? record.Abstract[..AbstractCharCap] + "..."
                    : record.Abstract;
                sb.Append(": ").Append(truncated);
            }
            sb.Append('\n');
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
