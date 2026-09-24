using RecordService.DataAccess.Embeddings;

namespace test;

/// <summary>
/// Returns a fixed 1536-length vector for every text, so tests exercise the embedding-storage
/// path without making real OpenAI calls.
/// </summary>
public class FakeEmbeddingClient : IEmbeddingClient
{
    // Non-zero: pgvector's cosine distance operator errors on a zero-norm vector.
    private static readonly float[] FixedEmbedding = Enumerable.Repeat(0.1f, 1536).ToArray();

    public Task<IReadOnlyList<float[]?>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        IReadOnlyList<float[]?> results = texts.Select(_ => FixedEmbedding).ToList();
        return Task.FromResult(results);
    }
}
