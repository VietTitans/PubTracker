using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RecordData;
using RecordService.BusinessLogic.UsersService;
using RecordService.DataAccess;
using RecordService.DataAccess.Keycloak;
using RecordService.Exceptions;

namespace test;

/// <summary>
/// Covers pushing a profile email change into Keycloak: the Admin API calls made by
/// KeycloakAdminUserSync (against a stubbed HTTP handler), and the ordering rules in
/// UsersService.UpdateUserAsync (Keycloak first, database only if Keycloak accepts).
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class KeycloakEmailSyncTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private const string RealmUrl = "http://keycloak:8080/auth/realms/science-alerts-saas";
    private readonly PubTrackerWebApplicationFactory _factory;

    public KeycloakEmailSyncTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateEmail_GetsAdminToken_ThenSendsFullUserWithNewEmailAndUsername()
    {
        var handler = new StubKeycloakHandler(
            userJson: """{"id":"sub-1","username":"old@example.com","email":"old@example.com","firstName":"Ada","attributes":{"x":["y"]}}""");
        var sync = new KeycloakAdminUserSync(new HttpClient(handler), RealmUrl, "pubtracker-api-admin", "secret");

        await sync.UpdateEmailAsync("sub-1", "new@example.com");

        Assert.Equal($"{RealmUrl}/protocol/openid-connect/token", handler.Requests[0].Url);
        Assert.Contains("grant_type=client_credentials", handler.Requests[0].Body);
        Assert.Contains("client_id=pubtracker-api-admin", handler.Requests[0].Body);
        Assert.Contains("client_secret=secret", handler.Requests[0].Body);

        const string adminUser = "http://keycloak:8080/auth/admin/realms/science-alerts-saas/users/sub-1";
        Assert.Equal(("GET", adminUser, "Bearer admin-token"), (handler.Requests[1].Method, handler.Requests[1].Url, handler.Requests[1].Authorization));
        Assert.Equal(("PUT", adminUser, "Bearer admin-token"), (handler.Requests[2].Method, handler.Requests[2].Url, handler.Requests[2].Authorization));

        var sent = JsonNode.Parse(handler.Requests[2].Body)!;
        Assert.Equal("new@example.com", (string?)sent["email"]);
        Assert.Equal("new@example.com", (string?)sent["username"]); // email-as-username realm
        Assert.Equal("Ada", (string?)sent["firstName"]);            // rest of the account untouched
        Assert.Equal("y", (string?)sent["attributes"]!["x"]![0]);
    }

    [Fact]
    public async Task UpdateEmail_LeavesUsernameAlone_WhenItIsNotTheEmail()
    {
        var handler = new StubKeycloakHandler(
            userJson: """{"id":"sub-1","username":"ada","email":"old@example.com"}""");
        var sync = new KeycloakAdminUserSync(new HttpClient(handler), RealmUrl, "c", "s");

        await sync.UpdateEmailAsync("sub-1", "new@example.com");

        var sent = JsonNode.Parse(handler.Requests[2].Body)!;
        Assert.Equal("new@example.com", (string?)sent["email"]);
        Assert.Equal("ada", (string?)sent["username"]);
    }

    [Fact]
    public async Task UpdateEmail_Conflict_ThrowsEmailAlreadyInUse()
    {
        var handler = new StubKeycloakHandler(
            userJson: """{"id":"sub-1","username":"old@example.com","email":"old@example.com"}""",
            putStatus: HttpStatusCode.Conflict);
        var sync = new KeycloakAdminUserSync(new HttpClient(handler), RealmUrl, "c", "s");

        await Assert.ThrowsAsync<EmailAlreadyInUseException>(() => sync.UpdateEmailAsync("sub-1", "taken@example.com"));
    }

    [Fact]
    public async Task UpdateUser_EmailChanged_UpdatesKeycloakAndDatabase()
    {
        var (service, dataAccess, sync) = CreateService();
        var user = await dataAccess.CreateUserAsync(new User { Name = "A", Username = "a", Email = "kc-old@example.com" });

        await service.UpdateUserAsync(user.Id, new User { Name = "A", Username = "a", Email = "kc-new@example.com" }, "sub-abc");

        Assert.Equal(new[] { ("sub-abc", "kc-new@example.com") }, sync.Calls);
        Assert.Equal("kc-new@example.com", (await dataAccess.GetUserByIdAsync(user.Id)).Email);
    }

    [Fact]
    public async Task UpdateUser_EmailUnchanged_DoesNotTouchKeycloak()
    {
        var (service, dataAccess, sync) = CreateService();
        var user = await dataAccess.CreateUserAsync(new User { Name = "A", Username = "a", Email = "same@example.com" });

        await service.UpdateUserAsync(user.Id, new User { Name = "Renamed", Username = "a", Email = "same@example.com" }, "sub-abc");

        Assert.Empty(sync.Calls);
        Assert.Equal("Renamed", (await dataAccess.GetUserByIdAsync(user.Id)).Name);
    }

    [Fact]
    public async Task UpdateUser_WithoutKeycloakSub_OnlyUpdatesDatabase()
    {
        var (service, dataAccess, sync) = CreateService();
        var user = await dataAccess.CreateUserAsync(new User { Name = "A", Username = "a", Email = "nosub-old@example.com" });

        await service.UpdateUserAsync(user.Id, new User { Name = "A", Username = "a", Email = "nosub-new@example.com" });

        Assert.Empty(sync.Calls);
        Assert.Equal("nosub-new@example.com", (await dataAccess.GetUserByIdAsync(user.Id)).Email);
    }

    [Fact]
    public async Task UpdateUser_KeycloakRejects_LeavesDatabaseUnchanged()
    {
        var (service, dataAccess, sync) = CreateService();
        sync.ThrowOnUpdate = new EmailAlreadyInUseException("taken");
        var user = await dataAccess.CreateUserAsync(new User { Name = "A", Username = "a", Email = "keep@example.com" });

        await Assert.ThrowsAsync<EmailAlreadyInUseException>(() =>
            service.UpdateUserAsync(user.Id, new User { Name = "A", Username = "a", Email = "taken@example.com" }, "sub-abc"));

        Assert.Equal("keep@example.com", (await dataAccess.GetUserByIdAsync(user.Id)).Email);
    }

    private (UsersService Service, IUsersDataAccess DataAccess, FakeKeycloakUserSync Sync) CreateService()
    {
        var scope = _factory.Services.CreateScope();
        var dataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var sync = new FakeKeycloakUserSync();
        return (new UsersService(dataAccess, sync, NullLogger<UsersService>.Instance), dataAccess, sync);
    }

    private sealed class FakeKeycloakUserSync : IKeycloakUserSync
    {
        public readonly List<(string Sub, string Email)> Calls = new();
        public Exception? ThrowOnUpdate { get; set; }

        public Task UpdateEmailAsync(string keycloakSub, string newEmail)
        {
            if (ThrowOnUpdate != null)
            {
                throw ThrowOnUpdate;
            }
            Calls.Add((keycloakSub, newEmail));
            return Task.CompletedTask;
        }
    }

    private sealed class StubKeycloakHandler : HttpMessageHandler
    {
        private readonly string _userJson;
        private readonly HttpStatusCode _putStatus;
        public readonly List<(string Method, string Url, string? Authorization, string Body)> Requests = new();

        public StubKeycloakHandler(string userJson, HttpStatusCode putStatus = HttpStatusCode.NoContent)
        {
            _userJson = userJson;
            _putStatus = putStatus;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), body));

            if (request.RequestUri.AbsolutePath.EndsWith("/protocol/openid-connect/token"))
            {
                return Json("""{"access_token":"admin-token"}""");
            }
            return request.Method == HttpMethod.Get ? Json(_userJson) : new HttpResponseMessage(_putStatus);
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
