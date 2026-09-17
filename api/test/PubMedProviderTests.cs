using System.Net;
using RecordService.DataAccess.ExternalSources;

namespace test;

/// <summary>
/// Exercises PubMedProvider.SearchAsync end-to-end against canned NCBI E-utilities XML
/// responses, via a fake HttpMessageHandler, so no real network access is needed.
/// </summary>
public class PubMedProviderTests
{
    private const string SearchUrl = "https://pubmed.ncbi.nlm.nih.gov/?term=covid+vaccine";

    private const string EsearchXml = """
        <?xml version="1.0"?>
        <eSearchResult>
          <Count>3</Count>
          <RetMax>3</RetMax>
          <RetStart>0</RetStart>
          <IdList>
            <Id>111</Id>
            <Id>222</Id>
            <Id>333</Id>
          </IdList>
        </eSearchResult>
        """;

    private const string EfetchXml = """
        <?xml version="1.0"?>
        <PubmedArticleSet>
          <PubmedArticle>
            <MedlineCitation>
              <PMID>111</PMID>
              <Article>
                <ArticleTitle>Title One</ArticleTitle>
                <AuthorList>
                  <Author><ForeName>Jane</ForeName><LastName>Doe</LastName></Author>
                  <Author><ForeName>John</ForeName><LastName>Smith</LastName></Author>
                </AuthorList>
                <Abstract><AbstractText>Abstract one text.</AbstractText></Abstract>
                <Journal><JournalIssue><PubDate><Year>2021</Year><Month>Jun</Month><Day>15</Day></PubDate></JournalIssue></Journal>
              </Article>
            </MedlineCitation>
            <PubmedData>
              <ArticleIdList>
                <ArticleId IdType="doi">10.1000/xyz123</ArticleId>
                <ArticleId IdType="pubmed">111</ArticleId>
              </ArticleIdList>
            </PubmedData>
          </PubmedArticle>
          <PubmedArticle>
            <MedlineCitation>
              <PMID>222</PMID>
              <Article>
                <ArticleTitle>Title Two</ArticleTitle>
                <AuthorList>
                  <Author><ForeName>Alice</ForeName><LastName>Wong</LastName></Author>
                </AuthorList>
                <Journal><JournalIssue><PubDate><Year>2019</Year></PubDate></JournalIssue></Journal>
              </Article>
            </MedlineCitation>
            <PubmedData>
              <ArticleIdList>
                <ArticleId IdType="pubmed">222</ArticleId>
              </ArticleIdList>
            </PubmedData>
          </PubmedArticle>
          <PubmedArticle>
            <MedlineCitation>
              <PMID>333</PMID>
              <Article>
                <ArticleTitle>Title Three</ArticleTitle>
                <Journal><JournalIssue><PubDate><MedlineDate>2020 Jan-Feb</MedlineDate></PubDate></JournalIssue></Journal>
              </Article>
            </MedlineCitation>
          </PubmedArticle>
        </PubmedArticleSet>
        """;

    private static PubMedProvider CreateProvider(HttpMessageHandler handler) =>
        new(new HttpClient(handler), apiKey: null, contactEmail: null);

    [Fact]
    public async Task SearchAsync_ParsesDoiTitleAuthorsAbstractAndPubDate()
    {
        var provider = CreateProvider(new FakeEutilsHandler(EsearchXml, EfetchXml));

        var result = await provider.SearchAsync(SearchUrl);

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.Equal(3, result.NewRecords.Count);

        var first = result.NewRecords[0];
        Assert.Equal("pubmed:111", first.ExternalId);
        Assert.Equal("10.1000/xyz123", first.Doi);
        Assert.Equal("Title One", first.Title);
        Assert.Equal("Jane Doe, John Smith", first.Authors);
        Assert.Equal("Abstract one text.", first.Abstract);
        Assert.Equal(new DateTime(2021, 6, 15), first.PublishedDate);
    }

    [Fact]
    public async Task SearchAsync_LeavesDoiNullWhenMissing()
    {
        var provider = CreateProvider(new FakeEutilsHandler(EsearchXml, EfetchXml));

        var result = await provider.SearchAsync(SearchUrl);

        var second = result.NewRecords[1];
        Assert.Equal("pubmed:222", second.ExternalId);
        Assert.Null(second.Doi);
        Assert.Equal("Alice Wong", second.Authors);
        Assert.Equal(new DateTime(2019, 1, 1), second.PublishedDate);
    }

    [Fact]
    public async Task SearchAsync_ExtractsYearFromMedlineDateFallback()
    {
        var provider = CreateProvider(new FakeEutilsHandler(EsearchXml, EfetchXml));

        var result = await provider.SearchAsync(SearchUrl);

        var third = result.NewRecords[2];
        Assert.Equal("pubmed:333", third.ExternalId);
        Assert.Null(third.Doi);
        Assert.Equal(new DateTime(2020, 1, 1), third.PublishedDate);
    }

    [Fact]
    public async Task SearchAsync_ParsesPubmedBookArticle()
    {
        const string bookEsearchXml = """
            <?xml version="1.0"?>
            <eSearchResult>
              <Count>1</Count>
              <RetMax>1</RetMax>
              <RetStart>0</RetStart>
              <IdList>
                <Id>444</Id>
              </IdList>
            </eSearchResult>
            """;

        const string bookEfetchXml = """
            <?xml version="1.0"?>
            <PubmedArticleSet>
              <PubmedBookArticle>
                <BookDocument>
                  <PMID>444</PMID>
                  <ArticleTitle>Lateral Epicondylitis</ArticleTitle>
                  <AuthorList>
                    <Author><ForeName>Jane</ForeName><LastName>Doe</LastName></Author>
                  </AuthorList>
                  <Book>
                    <PubDate><Year>2026</Year><Month>01</Month></PubDate>
                  </Book>
                  <Abstract><AbstractText>Book abstract text.</AbstractText></Abstract>
                </BookDocument>
                <PubmedBookData>
                  <ArticleIdList>
                    <ArticleId IdType="pubmed">444</ArticleId>
                  </ArticleIdList>
                </PubmedBookData>
              </PubmedBookArticle>
            </PubmedArticleSet>
            """;

        var provider = CreateProvider(new FakeEutilsHandler(bookEsearchXml, bookEfetchXml));

        var result = await provider.SearchAsync(SearchUrl);

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        var record = Assert.Single(result.NewRecords);
        Assert.Equal("pubmed:444", record.ExternalId);
        Assert.Null(record.Doi);
        Assert.Equal("Lateral Epicondylitis", record.Title);
        Assert.Equal("Jane Doe", record.Authors);
        Assert.Equal("Book abstract text.", record.Abstract);
        Assert.Equal(new DateTime(2026, 1, 1), record.PublishedDate);
    }

    [Fact]
    public async Task SearchAsync_ReturnsUnsuccessful_WhenEutilsRequestFails()
    {
        var provider = CreateProvider(new FaultingHandler());

        var result = await provider.SearchAsync(SearchUrl);

        Assert.False(result.IsSuccessful);
        Assert.Empty(result.NewRecords);
    }

    // Regression test for a real bug: NCBI's non-history ESearch mode caps out at 9,999
    // records, so a second page requested past that ceiling returns an <ERROR> element
    // instead of <Count>. The parser used to unconditionally reassign `total` from each
    // page's <Count>, so that error page reset the real total (14089) back to 0 even
    // though the first page's records were still fetched successfully.
    [Fact]
    public async Task SearchAsync_KeepsTotalFromFirstPage_WhenSecondPageHitsRetstartCeiling()
    {
        const string firstPageEsearchXml = """
            <?xml version="1.0"?>
            <eSearchResult>
              <Count>14089</Count>
              <RetMax>1</RetMax>
              <RetStart>0</RetStart>
              <IdList>
                <Id>111</Id>
              </IdList>
            </eSearchResult>
            """;

        const string secondPageErrorXml = """
            <?xml version="1.0"?>
            <eSearchResult>
              <ERROR>Search Backend failed: Exception:
              'retstart' cannot be larger than 9998.</ERROR>
            </eSearchResult>
            """;

        var provider = CreateProvider(new PagedEsearchHandler(firstPageEsearchXml, secondPageErrorXml, EfetchXml));

        var result = await provider.SearchAsync(SearchUrl);

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.Equal(14089, result.TotalRecordCount);
    }

    private class FakeEutilsHandler(string esearchXml, string efetchXml) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var xml = request.RequestUri!.AbsolutePath.EndsWith("esearch.fcgi") ? esearchXml : efetchXml;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) });
        }
    }

    private class FaultingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    private class PagedEsearchHandler(string firstEsearchXml, string secondEsearchXml, string efetchXml) : HttpMessageHandler
    {
        private int _esearchCallCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("esearch.fcgi"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(efetchXml) });
            }

            _esearchCallCount++;
            var xml = _esearchCallCount == 1 ? firstEsearchXml : secondEsearchXml;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) });
        }
    }
}
