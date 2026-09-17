namespace RecordService.DataAccess.ExternalSources;

/// <summary>
/// Factory pattern for creating literature source providers.
/// Automatically selects the correct provider based on URL detection.
/// Enables easy addition of new providers in the future.
/// </summary>
public class LiteratureSourceFactory
{
    private readonly IEnumerable<ILiteratureSourceProvider> _providers;

    public LiteratureSourceFactory(IEnumerable<ILiteratureSourceProvider> providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <summary>
    /// Creates/resolves the appropriate provider for the given URL.
    /// </summary>
    /// <param name="url">The literature source URL</param>
    /// <returns>The matching provider, or null if no provider can handle the URL</returns>
    /// <exception cref="InvalidOperationException">Thrown if no provider can handle the URL</exception>
    public ILiteratureSourceProvider CreateProvider(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty", nameof(url));

        var provider = _providers.FirstOrDefault(p => p.CanHandle(url));

        if (provider == null)
        {
            var sourceType = SourceDetector.DetectSource(url);
            throw new InvalidOperationException(
                $"No provider found for URL: {url}. Detected source: {SourceDetector.GetProviderName(sourceType)}. " +
                $"Available providers: {string.Join(", ", _providers.Select(p => p.ProviderName))}"
            );
        }

        return provider;
    }

    /// <summary>
    /// Tries to create a provider for the given URL without throwing exceptions.
    /// </summary>
    /// <param name="url">The literature source URL</param>
    /// <param name="provider">The matching provider, or null if not found</param>
    /// <returns>True if a provider was found, false otherwise</returns>
    public bool TryCreateProvider(string url, out ILiteratureSourceProvider? provider)
    {
        provider = null;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        provider = _providers.FirstOrDefault(p => p.CanHandle(url));
        return provider != null;
    }

    /// <summary>
    /// Gets list of all supported source types
    /// </summary>
    public IEnumerable<string> GetSupportedSources()
    {
        return _providers.Select(p => p.ProviderName);
    }
}
