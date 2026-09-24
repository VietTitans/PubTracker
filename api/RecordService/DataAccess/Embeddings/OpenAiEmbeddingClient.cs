using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RecordService.DataAccess.Embeddings;

/// <summary>
/// OpenAI embeddings provider. Used to embed record text (for retrieval) and chat questions
/// (for the query side of the same similarity search).
/// </summary>
public class OpenAiEmbeddingClient : IEmbeddingClient
{
    private const string Model = "text-embedding-3-small";
    private const int BatchSize = 100; // keep individual request payloads to a reasonable size

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiEmbeddingClient> _logger;

    public OpenAiEmbeddingClient(HttpClient httpClient, string apiKey, ILogger<OpenAiEmbeddingClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey ?? throw new ArgumentNullException(nameof(apiKey)));
    }

    public async Task<IReadOnlyList<float[]?>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var results = new float[]?[texts.Count];

        for (var i = 0; i < texts.Count; i += BatchSize)
        {
            var batch = texts.Skip(i).Take(BatchSize).ToList();

            try
            {
                var response = await _httpClient.PostAsJsonAsync("embeddings", new { model = Model, input = batch }, ct);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadFromJsonAsync<EmbeddingsResponse>(cancellationToken: ct);
                foreach (var item in body?.Data ?? [])
                {
                    results[i + item.Index] = item.Embedding;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI embedding request failed for batch starting at index {BatchStart}", i);
            }
        }

        return results;
    }

    private class EmbeddingsResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingItem> Data { get; set; } = new();
    }

    private class EmbeddingItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
