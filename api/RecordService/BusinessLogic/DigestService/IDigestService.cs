using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

public interface IDigestService
{
    /// <summary>
    /// Renders one combined email for a user, with one section per PendingUserDigest (not one email
    /// per query). Does not send: the caller queues the result in the digest outbox.
    /// </summary>
    /// <returns>Subject and HTML body, or null if the user is gone or marked for deletion (no one to send to).</returns>
    Task<(string Subject, string HtmlBody)?> BuildCombinedDigestAsync(int userId, IReadOnlyList<PendingUserDigest> userQueries);
}
