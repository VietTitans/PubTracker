using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecordService.DataAccess.ExternalSources;
using RecordService.Sources;
using Xunit;

namespace Test;

public class SourceCatalogTests
{
    private static LiteratureSourceFactory Build(params (string Key, string? Value)[] settings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value))
            .Build();
        var services = new ServiceCollection().AddLogging().AddLiteratureSources(config);
        return services.BuildServiceProvider().GetRequiredService<LiteratureSourceFactory>();
    }

    [Fact]
    public void Defaults_EnablePubMed_DisablePedro()
    {
        Assert.Equal(["PubMed"], Build().GetSupportedSources());
    }

    [Fact]
    public void BlankFlag_FallsBackToDefault()
    {
        Assert.Equal(["PubMed"], Build(("Sources:PubMed:Enabled", ""), ("Sources:Pedro:Enabled", "")).GetSupportedSources());
    }

    [Fact]
    public void PedroFlagTrue_EnablesPedro()
    {
        Assert.Contains("PEDro", Build(("Sources:Pedro:Enabled", "true")).GetSupportedSources());
    }

    [Fact]
    public void DisabledSource_ErrorNamesTheFlag()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Build().CreateProvider("https://search.pedro.org.au/search-results?abstract_with_title=x"));
        Assert.Contains("Sources:Pedro:Enabled", ex.Message);
    }
}
