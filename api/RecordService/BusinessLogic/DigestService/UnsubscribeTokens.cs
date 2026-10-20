using System.Security.Cryptography;
using System.Text;

namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Signed, stateless tokens for the unsubscribe links in a Digest. A token is
/// base64url(payload) + "." + base64url(HMAC-SHA256(payload)), where the payload is
/// "u{userId}" (stop all Digests) or "u{userId}:q{searchQueryId}" (leave one Subscription).
/// The signature binds the token to its user and scope, so it cannot be edited to target
/// another user. Tokens carry no expiry: an old email must keep working.
/// </summary>
public class UnsubscribeTokens
{
    private readonly byte[] _key;
    private readonly string _baseUrl;

    /// <param name="baseUrl">Public origin the API is reachable at; links are {baseUrl}/api/v1/subscriptions/unsubscribe/{token}.</param>
    public UnsubscribeTokens(string signingKey, string baseUrl)
    {
        _key = Encoding.UTF8.GetBytes(signingKey);
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public string StopAllUrl(int userId) => $"{_baseUrl}/api/v1/subscriptions/unsubscribe/{CreateStopAll(userId)}";

    public string SubscriptionUrl(int userId, int searchQueryId) => $"{_baseUrl}/api/v1/subscriptions/unsubscribe/{CreateForSubscription(userId, searchQueryId)}";

    public string CreateStopAll(int userId) => Sign($"u{userId}");

    public string CreateForSubscription(int userId, int searchQueryId) => Sign($"u{userId}:q{searchQueryId}");

    /// <returns>False if the token is malformed or its signature does not match.</returns>
    public bool TryValidate(string token, out int userId, out int? searchQueryId)
    {
        userId = 0;
        searchQueryId = null;

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(parts[0]);
            if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(_key, payloadBytes), Base64UrlDecode(parts[1])))
            {
                return false;
            }

            var payload = Encoding.UTF8.GetString(payloadBytes).Split(':');
            if (payload.Length is < 1 or > 2 || !payload[0].StartsWith('u') || !int.TryParse(payload[0][1..], out userId))
            {
                return false;
            }

            if (payload.Length == 2)
            {
                if (!payload[1].StartsWith('q') || !int.TryParse(payload[1][1..], out var queryId))
                {
                    return false;
                }
                searchQueryId = queryId;
            }
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private string Sign(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Base64UrlEncode(bytes) + "." + Base64UrlEncode(HMACSHA256.HashData(_key, bytes));
    }

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}
