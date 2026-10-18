using Themia.Core.Accounts;
using Themia.Sdk;
using Themia.Sdk.Services;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-9919] [S-9924] The device code, the pasted token and the accounts they make, on their pure side.</summary>
public sealed class ServiceSignInTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    // ---- the device code session (restored from the GitHub sign-in of before, e09beef^) ----------

    [Theory]
    [InlineData("authorization_pending", false, DevicePoll.Pending)]
    [InlineData("slow_down", false, DevicePoll.SlowDown)]
    [InlineData("expired_token", false, DevicePoll.Expired)]
    [InlineData("access_denied", false, DevicePoll.Denied)]
    [InlineData("authorization_declined", false, DevicePoll.Denied)]
    [InlineData("bad_verification_code", false, DevicePoll.Failed)]
    [InlineData(null, true, DevicePoll.Success)]
    [InlineData(null, false, DevicePoll.Failed)]
    public void S9919_token_endpoint_answers_are_classified(string? error, bool hasToken, DevicePoll expected)
        => Assert.Equal(expected, DeviceCodeSession.Classify(error, hasToken));

    [Fact]
    public void S9919_pending_waits_the_server_interval_and_slow_down_adds_five_seconds_for_good()
    {
        var session = new DeviceCodeSession("ABCD-1234", new Uri("https://device.example/login"), 5, 900, Now);

        var pending = session.Next(DevicePoll.Pending, Now);
        Assert.False(pending.Done);
        Assert.Equal(TimeSpan.FromSeconds(5), pending.Delay);

        var slow = session.Next(DevicePoll.SlowDown, Now);
        Assert.Equal(TimeSpan.FromSeconds(10), slow.Delay);

        // The longer interval sticks: RFC 8628 says "for all subsequent requests".
        Assert.Equal(TimeSpan.FromSeconds(10), session.Next(DevicePoll.Pending, Now).Delay);
    }

    [Fact]
    public void S9919_polling_stops_when_the_code_has_expired()
    {
        var session = new DeviceCodeSession("A", new Uri("https://device.example/login"), 5, 900, Now);

        var step = session.Next(DevicePoll.Pending, Now.AddSeconds(900));

        Assert.True(step.Done);
        Assert.Equal(DevicePoll.Expired, step.Poll);
    }

    [Theory]
    [InlineData(DevicePoll.Success)]
    [InlineData(DevicePoll.Expired)]
    [InlineData(DevicePoll.Denied)]
    [InlineData(DevicePoll.Failed)]
    public void S9919_terminal_outcomes_end_the_flow(DevicePoll poll)
    {
        var session = new DeviceCodeSession("A", new Uri("https://x.example"), 5, 900, Now);
        var step = session.Next(poll, Now, "bad_verification_code");

        Assert.True(step.Done);
        Assert.Equal(TimeSpan.Zero, step.Delay);
        Assert.Equal(poll == DevicePoll.Success, step.Message is null);
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData(0L, 5)]
    [InlineData(100_000L, 5)]
    [InlineData(8L, 8)]
    public void S9919_a_missing_or_absurd_interval_falls_back_to_five_seconds(long? interval, int expected)
        => Assert.Equal(TimeSpan.FromSeconds(expected), new DeviceCodeSession("A", new Uri("https://x.example"), interval, null, Now).Interval);

    // ---- the device authorization answer ------------------------------------------------------------

    [Fact]
    public void S9919_a_device_grant_needs_every_field_and_an_https_page()
    {
        var grant = DeviceGrant.Parse(200, """{"device_code":"dc","user_code":"WDJB-MJHT","verification_uri":"https://device.example/device","expires_in":900,"interval":"5"}""");

        Assert.Equal(new DeviceGrant("dc", "WDJB-MJHT", new Uri("https://device.example/device"), 5, 900), grant);
        Assert.Null(DeviceGrant.Parse(200, """{"device_code":"dc","user_code":"X","verification_uri":"http://device.example/device"}"""));
        Assert.Null(DeviceGrant.Parse(200, """{"user_code":"X","verification_uri":"https://device.example/device"}"""));
        Assert.Null(DeviceGrant.Parse(400, """{"device_code":"dc","user_code":"X","verification_uri":"https://device.example/device"}"""));
        Assert.Null(DeviceGrant.Parse(200, "<html/>"));
        Assert.NotNull(DeviceGrant.Parse(200, """{"device_code":"dc","user_code":"X","verification_url":"https://device.example/device"}""")); // the older spelling
    }

    private static readonly OAuthDeviceCode Device = new(
        new("https://device.example/login/device/code"), new("https://device.example/login/oauth/access_token"), "client-1",
        ["read:user", "repo"], ScopeSeparator.Space, new RefreshRule(true, TimeSpan.Zero), new("https://device.example/apps/x/installations/new"));

    [Fact]
    public void S9918_the_device_codes_requests_are_its_data()
    {
        Assert.Equal(["client_id=client-1", "scope=read:user repo"], Pairs(OAuthRequests.DeviceAuthorization(Device)));
        Assert.Equal(["client_id=client-1"], Pairs(OAuthRequests.DeviceAuthorization(Device with { Scopes = [] })));
        Assert.Equal(["client_id=client-1", "device_code=DC", "grant_type=urn:ietf:params:oauth:grant-type:device_code"], Pairs(OAuthRequests.DevicePoll(Device, "DC")));
        Assert.Equal(["client_id=client-1", "grant_type=refresh_token", "refresh_token=RT"], Pairs(OAuthRequests.Refresh(Device, "RT", null)));
        Assert.Throws<ArgumentException>(() => OAuthRequests.Refresh(new PastedToken(new("https://x.example"), new TokenShape(1, 9, TokenShape.Word)), "RT", null));
    }

    [Fact]
    public void S9911_a_token_goes_bearer_or_bare_as_its_member_says()
    {
        Assert.Equal("Bearer abc", OAuthRequests.AuthorizationValue(TokenScheme.Bearer, "abc"));
        Assert.Equal("abc", OAuthRequests.AuthorizationValue(TokenScheme.Raw, "abc"));
        Assert.Equal(TokenScheme.Raw, new PastedToken(new("https://x.example"), new TokenShape(1, 9, TokenShape.Word), TokenScheme.Raw).Scheme);
        Assert.Equal(TokenScheme.Bearer, Device.Scheme);
    }

    private static string[] Pairs(IReadOnlyList<KeyValuePair<string, string>> fields) => [.. fields.Select(f => $"{f.Key}={f.Value}")];

    // ---- the accounts they make -----------------------------------------------------------------------

    [Theory]
    [InlineData("browser", SignInKind.Browser)]
    [InlineData("deviceCode", SignInKind.DeviceCode)]
    [InlineData("pastedToken", SignInKind.PastedToken)]
    public void S9919_a_sign_in_kind_is_written_and_read_by_its_wire_name(string wire, SignInKind kind)
    {
        Assert.Equal(wire, SignInKinds.Wire(kind));
        Assert.True(SignInKinds.TryParse(wire, out var read));
        Assert.Equal(kind, read);
        Assert.False(SignInKinds.TryParse("token", out _));
    }

    [Fact]
    public void S9919_an_account_keeps_only_a_sign_in_kind_its_provider_offers()
    {
        var repairs = new List<string>();
        var stored = new StoredAccount { Id = "ms-1", Provider = "microsoft", Address = "ada@example.com", SignIn = "browser" };

        Assert.Equal(SignInKind.Browser, AccountRules.Validate(stored, Now, repairs)!.SignedInWith);
        Assert.Empty(repairs);

        stored.SignIn = "pastedToken";
        var account = AccountRules.Validate(stored, Now, repairs)!;
        Assert.Null(account.SignedInWith);
        Assert.Single(repairs);

        stored.SignIn = null;
        Assert.Null(AccountRules.Validate(stored, Now, repairs)!.SignedInWith);
        Assert.Null(StoredAccount.From(AccountRules.Validate(stored, Now, repairs)!).SignIn); // nothing written for the provider's only flow
    }

    [Fact]
    public void S9919_a_pasted_token_never_renews_and_a_member_says_how_an_account_signs_requests()
    {
        var browser = new Account { Id = "ms-1", Provider = HostProviders.Microsoft, Address = "a@example.com" };
        var pasted = browser with { SignedInWith = SignInKind.PastedToken };

        Assert.Equal(AccountAuth.OAuthRefresh, AccountRules.AuthOf(browser));
        Assert.Equal(AccountAuth.Token, AccountRules.AuthOf(pasted));
        Assert.Equal(AccountAuth.Password, AccountRules.AuthOf(new Account { Id = "i", Provider = HostProviders.Imap, Address = "a@example.com" }));

        Assert.Same(OAuthClients.Microsoft.SignIn, AccountRules.MemberOf(browser));
        Assert.Null(AccountRules.MemberOf(pasted)); // Microsoft offers no pasted token
        Assert.Null(AccountRules.MemberOf(new Account { Id = "i", Provider = HostProviders.Imap, Address = "a@example.com" }));
    }

    [Fact]
    public void S9919_a_service_accounts_id_is_its_provider_and_the_id_its_identity_named()
        => Assert.Equal("caldav:4242", AccountIds.Service(HostProviders.CalDav, "4242"));

    [Fact]
    public void S9916_a_numeric_id_is_read_as_its_text()
    {
        var endpoint = new IdentityEndpoint(new("https://api.device.example/user"), "id", ["login"], "name", "x:");
        Assert.Equal(new ServiceIdentity("x:583231", "octo", "Octo Cat"), IdentityAnswers.Read(endpoint, """{"id":583231,"login":"octo","name":"Octo Cat"}"""));
    }

    // ---- [S-9924] an account's own API root ---------------------------------------------------------------

    [Fact]
    public void S9924_a_request_moves_onto_the_accounts_own_root_and_nowhere_else()
    {
        var api = new Uri("https://api.hub.example/");
        var own = new Uri("https://git.corp.example/api/v3/");

        Assert.Equal(new Uri("https://git.corp.example/api/v3/repos/a/b?x=1"), ServiceRoots.Rebase(new("https://api.hub.example/repos/a/b?x=1"), api, own));
        Assert.Equal(new Uri("https://git.corp.example/api/v3/"), ServiceRoots.Rebase(new("https://api.hub.example/"), api, own));
        Assert.Null(ServiceRoots.Rebase(new("https://evil.example/repos"), api, own));
        Assert.Null(ServiceRoots.Rebase(new("https://api.hub.example/x"), api, new("http://git.corp.example/")));
    }

    [Theory]
    [InlineData("https://git.corp.example/api/v3/", true)]
    [InlineData("http://git.corp.example/", false)]
    [InlineData("https://localhost/", false)]
    [InlineData("https://192.168.1.4/", false)]
    [InlineData("https://user:pw@git.corp.example/", false)]
    [InlineData("https://intranet/", false)]
    public void S9924_an_accounts_own_root_is_https_on_a_public_name(string root, bool acceptable)
        => Assert.Equal(acceptable, ServiceRoots.IsAcceptable(new Uri(root)));
}
