using RecordService.BusinessLogic.DigestService;

namespace test;

public class UnsubscribeTokensTests
{
    private static readonly UnsubscribeTokens Tokens = new("key-one", "http://x");

    [Fact]
    public void StopAllToken_RoundTrips()
    {
        Assert.True(Tokens.TryValidate(Tokens.CreateStopAll(7), out var userId, out var queryId));
        Assert.Equal(7, userId);
        Assert.Null(queryId);
    }

    [Fact]
    public void SubscriptionToken_RoundTrips()
    {
        Assert.True(Tokens.TryValidate(Tokens.CreateForSubscription(7, 42), out var userId, out var queryId));
        Assert.Equal(7, userId);
        Assert.Equal(42, queryId);
    }

    [Fact]
    public void TokenWithSwappedPayload_IsRejected()
    {
        // Payload of user 8's token + signature of user 7's token.
        var forged = Tokens.CreateStopAll(8).Split('.')[0] + "." + Tokens.CreateStopAll(7).Split('.')[1];
        Assert.False(Tokens.TryValidate(forged, out _, out _));
    }

    [Fact]
    public void TokenSignedWithOtherKey_IsRejected()
    {
        var other = new UnsubscribeTokens("key-two", "http://x");
        Assert.False(Tokens.TryValidate(other.CreateStopAll(7), out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    public void MalformedToken_IsRejected(string token)
    {
        Assert.False(Tokens.TryValidate(token, out _, out _));
    }
}
