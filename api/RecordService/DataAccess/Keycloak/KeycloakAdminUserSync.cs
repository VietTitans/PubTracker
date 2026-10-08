using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using RecordService.Exceptions;

namespace RecordService.DataAccess.Keycloak;

/// <summary>
/// Updates a user's email through Keycloak's Admin REST API, authenticated as a service account
/// (client-credentials grant) that holds the realm-management roles view-users and manage-users.
/// </summary>
public class KeycloakAdminUserSync : IKeycloakUserSync
{
    private readonly HttpClient _httpClient;
    private readonly string _realmUrl;
    private readonly string _adminRealmUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;

    /// <param name="realmUrl">Realm base URL reachable from this process, e.g. http://keycloak:8080/auth/realms/science-alerts-saas</param>
    public KeycloakAdminUserSync(HttpClient httpClient, string realmUrl, string clientId, string clientSecret)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _realmUrl = (realmUrl ?? throw new ArgumentNullException(nameof(realmUrl))).TrimEnd('/');
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));

        // .../auth/realms/{realm} -> .../auth/admin/realms/{realm}
        var realmsIndex = _realmUrl.LastIndexOf("/realms/", StringComparison.Ordinal);
        if (realmsIndex < 0)
        {
            throw new ArgumentException("Realm URL must contain /realms/.", nameof(realmUrl));
        }
        _adminRealmUrl = _realmUrl[..realmsIndex] + "/admin" + _realmUrl[realmsIndex..];
    }

    public async Task UpdateEmailAsync(string keycloakSub, string newEmail)
    {
        var token = await GetAdminTokenAsync();
        var userUrl = $"{_adminRealmUrl}/users/{Uri.EscapeDataString(keycloakSub)}";

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, userUrl);
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var getResponse = await _httpClient.SendAsync(getRequest);
        await EnsureSuccessAsync(getResponse, "load user");

        // Send back the full representation Keycloak gave us, so nothing else on the account
        // (names, attributes, required actions) is cleared by the update.
        var user = await getResponse.Content.ReadFromJsonAsync<JsonObject>()
            ?? throw new HttpRequestException("Keycloak returned an empty user representation.");

        // Username is left alone (the realm does not use email-as-username), so the old login keeps working.
        user["email"] = newEmail;

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, userUrl)
        {
            Content = JsonContent.Create(user)
        };
        putRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var putResponse = await _httpClient.SendAsync(putRequest);

        if (putResponse.StatusCode == HttpStatusCode.Conflict)
        {
            throw new EmailAlreadyInUseException("That email is already used by another account.");
        }
        await EnsureSuccessAsync(putResponse, "update user");
    }

    private async Task<string> GetAdminTokenAsync()
    {
        using var response = await _httpClient.PostAsync(
            $"{_realmUrl}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            }));
        await EnsureSuccessAsync(response, "get admin token");

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        return body?["access_token"]?.GetValue<string>()
            ?? throw new HttpRequestException("Keycloak token response had no access_token.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Keycloak {action} failed ({(int)response.StatusCode}): {body}");
        }
    }
}
