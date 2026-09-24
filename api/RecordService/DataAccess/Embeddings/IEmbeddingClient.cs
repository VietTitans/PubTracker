namespace RecordService.DataAccess.Embeddings;

public interface IEmbeddingClient
{
    /// <summary>
    /// Embeds each text and returns a list aligned 1:1 with <paramref name="texts"/>. A chunk-level
    /// API failure yields null for that chunk's entries (logged, not thrown) so one bad batch
    /// doesn't fail an entire poll.
    /// </summary>
    Task<IReadOnlyList<float[]?>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
