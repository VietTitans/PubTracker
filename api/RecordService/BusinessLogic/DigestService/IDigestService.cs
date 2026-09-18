using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

public interface IDigestService
{
    /// <summary>
    /// Sends exactly one combined email per distinct UserId in pendingDigests, with one
    /// section per PendingUserDigest belonging to that user - not one email per query.
    /// A failure sending to one user does not stop other users from being notified.
    /// </summary>
    /// <returns>
    /// Per (UserId, SearchQueryId) pair present in the input: true if that user's combined
    /// email sent successfully. A failure only marks the pairs in THAT user's email - other
    /// subscribers of the same query, and other queries in a different user's email, are
    /// unaffected.
    /// </returns>
    Task<IReadOnlyDictionary<(int UserId, int SearchQueryId), bool>> SendCombinedDigestsAsync(IReadOnlyList<PendingUserDigest> pendingDigests);
}
