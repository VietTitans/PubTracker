using RecordService.DataAccess.ExternalSources;

namespace test;

/// <summary>
/// Regression coverage for PedroProvider.CanHandle's host allow-list (search.pedro.org.au
/// only, https only), added after a security review found the previous "url contains 'pedro'"
/// check let a subscribed URL drive the provider's headless browser to any host - an SSRF
/// vector. No browser is launched here since CanHandle is a pure URL check.
/// </summary>
public class PedroProviderCanHandleTests
{
    [Theory]
    [InlineData("http://169.254.169.254/?pedro=1")] // cloud metadata endpoint, substring match only
    [InlineData("https://search.pedro.org.au.evil.com/")] // suffix trick, not the real host
    [InlineData("https://evil.com/?pedro=search.pedro.org.au")] // real host only in the query string
    [InlineData("file:///etc/passwd?pedro")] // non-http(s) scheme
    [InlineData("http://search.pedro.org.au/advanced-search/results")] // right host, wrong scheme
    public async Task CanHandle_RejectsUrlsOutsideTheAllowedHost(string url)
    {
        await using var provider = new PedroProvider();
        Assert.False(provider.CanHandle(url));
    }

    [Fact]
    public async Task CanHandle_AcceptsRealPedroSearchUrl()
    {
        await using var provider = new PedroProvider();
        Assert.True(provider.CanHandle(
            "https://search.pedro.org.au/advanced-search/results?body_part=VL01396&perpage=20"));
    }
}
