using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

public interface IDigestService
{
    /// <summary>
    /// Emails every subscriber of the given search query a digest of the newly found records.
    /// A failure sending to one subscriber does not stop the others from being notified.
    /// </summary>
    Task SendDigestForSearchQueryAsync(int searchQueryId, string targetUrl, IReadOnlyList<LiteratureRecord> newRecords);
}
