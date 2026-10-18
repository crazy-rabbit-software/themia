using Themia.Core.Accounts;
using Themia.Sdk;
using Themia.Sdk.Services;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The sign-in flows' pure parts: PKCE, the loopback redirect, the token exchange answer, GitHub's
/// device-code polling machine, the Claude paste, and the URLs each provider is sent to.
/// </summary>
public class AccountOAuthTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void S508_challenge_matches_the_rfc7636_vector()
        => Assert.Equal(
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            Pkce.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Fact]
    public void S508_verifier_is_unreserved_and_long_enough_and_challenge_unpadded()
    {
        var verifier = Pkce.CreateVerifier();

        Assert.InRange(verifier.Length, 43, 128);
        Assert.All(verifier, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~'));
        Assert.NotEqual(verifier, Pkce.CreateVerifier());

        var challenge = Pkce.Challenge("x");
        Assert.DoesNotContain('=', challenge);
        Assert.DoesNotContain('+', challenge);
        Assert.DoesNotContain('/', challenge);
    }

    [Fact]
    public void S508_state_comparison()
    {
        var state = Pkce.CreateState();

        Assert.True(Pkce.StateMatches(state, state));
        Assert.False(Pkce.StateMatches(state, state + "x"));
        Assert.False(Pkce.StateMatches(state, null));
        Assert.False(Pkce.StateMatches(string.Empty, string.Empty));
    }

    [Fact]
    public void S513_a_redirect_with_the_right_state_yields_the_code()
    {
        var redirect = OAuthRedirect.Parse("GET /?code=4%2F0Abc-d&state=xyz&scope=email HTTP/1.1", "xyz");

        Assert.Equal(RedirectKind.Code, redirect.Kind);
        Assert.Equal("4/0Abc-d", redirect.Code);
    }

    [Fact]
    public void S513_malformed_pairs_are_skipped_not_fatal()
    {
        var redirect = OAuthRedirect.Parse("GET /?&foo&bar=1&=x&code=abc&state=xyz HTTP/1.1", "xyz");

        Assert.Equal(RedirectKind.Code, redirect.Kind);
        Assert.Equal("abc", redirect.Code);
    }

    [Fact]
    public void S513_a_wrong_or_missing_state_is_refused_even_for_an_error()
    {
        Assert.Equal(RedirectKind.StateMismatch, OAuthRedirect.Parse("GET /?code=abc&state=other HTTP/1.1", "xyz").Kind);
        Assert.Equal(RedirectKind.StateMismatch, OAuthRedirect.Parse("GET /?code=abc HTTP/1.1", "xyz").Kind);
        Assert.Equal(RedirectKind.StateMismatch, OAuthRedirect.Parse("GET /?error=access_denied&state=other HTTP/1.1", "xyz").Kind);
    }

    [Fact]
    public void S513_a_provider_refusal_is_reported()
    {
        var redirect = OAuthRedirect.Parse("GET /?error=access_denied&state=xyz HTTP/1.1", "xyz");

        Assert.Equal(RedirectKind.ProviderError, redirect.Kind);
        Assert.Contains("cancelled", redirect.Error, StringComparison.Ordinal);

        var other = OAuthRedirect.Parse("GET /?error=invalid_scope&error_description=Bad+scope&state=xyz HTTP/1.1", "xyz");
        Assert.Equal("Bad scope", other.Error);
    }

    [Theory]
    [InlineData("GET /favicon.ico HTTP/1.1")]
    [InlineData("GET / HTTP/1.1")]
    [InlineData("GET /?unrelated=1 HTTP/1.1")]
    [InlineData("")]
    [InlineData(null)]
    public void S513_a_request_that_is_not_a_redirect_keeps_the_listener_waiting(string? line)
        => Assert.Equal(RedirectKind.Unrelated, OAuthRedirect.Parse(line, "xyz").Kind);

    [Fact]
    public void S509_microsoft_scopes_are_the_classic_ones()
    {
        foreach (var scope in new[] { "User.Read", "Mail.ReadWrite", "Calendars.Read", "Contacts.Read", "Tasks.ReadWrite", "offline_access" })
        {
            Assert.Contains(scope, OAuthClients.Microsoft.Scope.Split(' '));
        }

        // [S-1901] No widget reads OneDrive any more, so no file access is asked for.
        Assert.DoesNotContain("Files.Read", OAuthClients.Microsoft.Scope.Split(' '));

        Assert.Equal("6cf22c0f-103a-4f8a-ae35-178d62575897", OAuthClients.Microsoft.ClientId);
        Assert.Equal("https://login.microsoftonline.com/common/oauth2/v2.0/token", OAuthClients.Microsoft.TokenUrl.AbsoluteUri);
    }

    [Fact]
    public void S1780_microsoft_authorize_url_is_the_code_flow_with_pkce_and_account_choice()
    {
        var redirect = OAuthClients.Microsoft.SignIn.Loopback.RedirectUri(51234);
        var url = OAuthRequests.AuthorizeUrl(OAuthClients.Microsoft.SignIn, redirect, "STATE", "CHAL");

        Assert.Equal("http://localhost:51234/", redirect);
        Assert.Equal("https://login.microsoftonline.com/common/oauth2/v2.0/authorize", url.GetLeftPart(UriPartial.Path));

        var query = Query(url);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("6cf22c0f-103a-4f8a-ae35-178d62575897", query["client_id"]);
        Assert.Equal("http://localhost:51234/", query["redirect_uri"]);
        Assert.Equal("query", query["response_mode"]);
        Assert.Equal(OAuthClients.Microsoft.Scope, query["scope"]);
        Assert.Contains("offline_access", query["scope"].Split(' '));
        Assert.Equal("STATE", query["state"]);
        Assert.Equal("CHAL", query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("select_account", query["prompt"]);

        // A public client: nothing secret ever goes to the browser.
        Assert.DoesNotContain("client_secret", query.Keys);
    }

    [Fact]
    public void S1780_the_redirect_hosts_are_what_each_registration_accepts()
    {
        // Microsoft matches a "localhost" registration on any port, but not 127.0.0.1; Google takes the literal.
        Assert.Equal("http://localhost:8080/", OAuthClients.Microsoft.SignIn.Loopback.RedirectUri(8080));
        Assert.Equal("http://127.0.0.1:8080", OAuthClients.Google.SignIn.Loopback.RedirectUri(8080));
    }

    [Fact]
    public void S1781_a_microsoft_callback_with_the_issued_state_yields_the_code()
    {
        var redirect = OAuthRedirect.Parse(
            "GET /?code=M.C507_BAY.2.U.abc%21def&state=xyz&session_state=00000000-0000-0000-1111-000000000000 HTTP/1.1", "xyz");

        Assert.Equal(RedirectKind.Code, redirect.Kind);
        Assert.Equal("M.C507_BAY.2.U.abc!def", redirect.Code);
    }

    [Fact]
    public void S1781_a_microsoft_callback_with_another_state_is_refused()
        => Assert.Equal(RedirectKind.StateMismatch,
            OAuthRedirect.Parse("GET /?code=M.C507&state=forged&session_state=1 HTTP/1.1", "xyz").Kind);

    [Fact]
    public void S1781_a_microsoft_error_carries_its_description()
    {
        var declined = OAuthRedirect.Parse(
            "GET /?error=access_denied&error_description=The+user+has+denied+access+to+the+scope+requested+by+the+client+application.&state=xyz HTTP/1.1", "xyz");
        Assert.Equal(RedirectKind.ProviderError, declined.Kind);
        Assert.Contains("cancelled", declined.Error, StringComparison.Ordinal);

        var other = OAuthRedirect.Parse(
            "GET /?error=invalid_request&error_description=AADSTS50011%3A+The+redirect+URI+does+not+match.&state=xyz HTTP/1.1", "xyz");
        Assert.Equal(RedirectKind.ProviderError, other.Kind);
        Assert.Equal("AADSTS50011: The redirect URI does not match.", other.Error);
        Assert.Null(other.Code);
    }

    [Fact]
    public void S1781_a_callback_with_an_empty_code_is_an_error_not_a_sign_in()
    {
        var redirect = OAuthRedirect.Parse("GET /?code=&state=xyz HTTP/1.1", "xyz");

        Assert.Equal(RedirectKind.ProviderError, redirect.Kind);
        Assert.Null(redirect.Code);
        Assert.NotNull(redirect.Error);
    }

    [Fact]
    public void S1782_a_token_answer_with_an_access_token_is_a_grant()
    {
        var answer = CodeExchange.Parse(200,
            """{"token_type":"Bearer","scope":"User.Read","expires_in":3599,"ext_expires_in":3599,"access_token":"eyJ0.a.b","refresh_token":"M.R3_BAY.x"}""");

        Assert.Null(answer.Failure);
        Assert.NotNull(answer.Grant);
        Assert.Equal("eyJ0.a.b", answer.Grant.AccessToken);
        Assert.Equal("M.R3_BAY.x", answer.Grant.RefreshToken);
        Assert.Equal(3599, answer.Grant.ExpiresIn);
    }

    [Fact]
    public void S1782_expires_in_may_be_a_string_and_the_refresh_token_may_be_missing()
    {
        var answer = CodeExchange.Parse(200, """{"access_token":"abc","expires_in":"3600"}""");

        Assert.Equal(3600, answer.Grant!.ExpiresIn);
        Assert.Null(answer.Grant.RefreshToken);

        Assert.Null(CodeExchange.Parse(200, """{"access_token":"abc","expires_in":"soon"}""").Grant!.ExpiresIn);
    }

    [Fact]
    public void S1782_an_error_answer_fails_with_the_first_line_of_the_description()
    {
        var answer = CodeExchange.Parse(400,
            """{"error":"invalid_grant","error_description":"AADSTS54005: OAuth2 Authorization code was already redeemed.\r\nTrace ID: 1\r\nCorrelation ID: 2","error_codes":[54005]}""");

        Assert.Null(answer.Grant);
        Assert.Equal("AADSTS54005: OAuth2 Authorization code was already redeemed.", answer.Failure);
    }

    [Theory]
    [InlineData(200, """{"token_type":"Bearer"}""", "HTTP 200")]
    [InlineData(200, """{"access_token":""}""", "HTTP 200")]
    [InlineData(200, """{"access_token":42}""", "HTTP 200")]
    [InlineData(200, "<html>proxy</html>", "HTTP 200")]
    [InlineData(502, "", "HTTP 502")]
    [InlineData(400, """{"error":"invalid_client"}""", "invalid_client")]
    public void S1782_anything_but_a_2xx_with_a_token_is_a_failure(int status, string body, string failure)
    {
        var answer = CodeExchange.Parse(status, body);

        Assert.Null(answer.Grant);
        Assert.Equal(failure, answer.Failure);
    }

    [Fact]
    public void S1782_a_token_on_a_non_2xx_answer_is_not_believed()
        => Assert.Null(CodeExchange.Parse(401, """{"access_token":"abc"}""").Grant);

    private static Dictionary<string, string> Query(Uri url)
        => url.Query.TrimStart('?').Split('&')
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(kv => Uri.UnescapeDataString(kv[0]), kv => Uri.UnescapeDataString(kv[1]));

    [Fact]
    public void S509_google_authorize_url_carries_pkce_offline_access_and_the_loopback()
    {
        var url = OAuthRequests.AuthorizeUrl(OAuthClients.Google.SignIn, "http://127.0.0.1:51234", "STATE", "CHAL").AbsoluteUri;

        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", url, StringComparison.Ordinal);
        Assert.Contains("client_id=187748979097-url067h00d18pjbjgskddssdj0nrq04s.apps.googleusercontent.com", url, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A51234", url, StringComparison.Ordinal);
        Assert.Contains("code_challenge=CHAL", url, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", url, StringComparison.Ordinal);
        Assert.Contains("access_type=offline", url, StringComparison.Ordinal);
        Assert.Contains("prompt=consent", url, StringComparison.Ordinal);
        Assert.Contains("state=STATE", url, StringComparison.Ordinal);
        Assert.Contains("gmail.readonly", url, StringComparison.Ordinal);
    }

    // ---- [S-9910] [S-9916] [S-9918] the browser members of Microsoft and Google: the same requests as before ----

    private static string[] Pairs(IReadOnlyList<KeyValuePair<string, string>> fields) => [.. fields.Select(f => $"{f.Key}={f.Value}")];

    [Fact]
    public void S9918_the_microsoft_exchange_and_refresh_send_what_they_always_sent()
    {
        var member = OAuthClients.Microsoft.SignIn;

        Assert.Equal(
            ["grant_type=authorization_code", "code=CODE", "client_id=" + OAuthClients.Microsoft.ClientId, "code_verifier=VER", "redirect_uri=http://localhost:5050/", "scope=" + OAuthClients.Microsoft.Scope],
            Pairs(OAuthRequests.Exchange(member, "CODE", "VER", "http://localhost:5050/", secret: null)));

        Assert.Equal(
            ["client_id=" + OAuthClients.Microsoft.ClientId, "grant_type=refresh_token", "refresh_token=RT", "scope=" + OAuthClients.Microsoft.Scope],
            Pairs(OAuthRequests.Refresh(member, "RT", secret: null)));
    }

    [Fact]
    public void S9918_the_google_exchange_and_refresh_send_what_they_always_sent()
    {
        var member = OAuthClients.Google.SignIn;

        Assert.Equal(
            ["grant_type=authorization_code", "code=CODE", "client_id=" + OAuthClients.Google.ClientId, "client_secret=SECRET", "code_verifier=VER", "redirect_uri=http://127.0.0.1:5050"],
            Pairs(OAuthRequests.Exchange(member, "CODE", "VER", "http://127.0.0.1:5050", "SECRET")));

        Assert.Equal(
            ["client_id=" + OAuthClients.Google.ClientId, "client_secret=SECRET", "grant_type=refresh_token", "refresh_token=RT"],
            Pairs(OAuthRequests.Refresh(member, "RT", "SECRET")));

        Assert.Throws<ArgumentNullException>(() => OAuthRequests.Exchange(member, "CODE", "VER", "http://127.0.0.1:5050", secret: null));
    }

    [Fact]
    public void S9910_microsoft_and_google_offer_their_browser_member_and_only_google_needs_a_build_secret()
    {
        var microsoft = ProviderCatalog.Of(HostProviders.Microsoft);
        var google = ProviderCatalog.Of(HostProviders.Google);

        Assert.Same(OAuthClients.Microsoft.SignIn, Assert.Single(microsoft.SignIn.Members));
        Assert.Same(OAuthClients.Google.SignIn, Assert.Single(google.SignIn.Members));
        Assert.Equal(ClientSecretRule.None, OAuthClients.Microsoft.SignIn.ClientSecret);
        Assert.Equal(ClientSecretRule.BuildTime, OAuthClients.Google.SignIn.ClientSecret);
        Assert.Null(microsoft.BuildSecret);
        Assert.Equal("google", google.BuildSecret);
        Assert.True(ClientSecretRules.IsKey(google.BuildSecret));
        Assert.True(OAuthClients.Microsoft.SignIn.Loopback.AnyPort);
        Assert.True(OAuthClients.Google.SignIn.Loopback.AnyPort);

        foreach (var password in new[] { HostProviders.Imap, HostProviders.CalDav, HostProviders.CardDav })
        {
            Assert.Empty(ProviderCatalog.Of(password).SignIn.Members);
        }
    }

    [Fact]
    public void S9916_who_signed_in_to_microsoft_is_the_graph_id_its_mail_else_its_principal_name()
    {
        var endpoint = OAuthClients.Microsoft.SignIn.Identity!;
        Assert.Equal(OAuthClients.Microsoft.MeUrl, endpoint.Url);

        var mail = IdentityAnswers.Read(endpoint, """{"id":"abc","mail":"ada@example.com","displayName":"Ada","userPrincipalName":"ada@contoso.example"}""");
        Assert.Equal(new ServiceIdentity(AccountIds.Microsoft("abc"), "ada@example.com", "Ada"), mail);

        var upn = IdentityAnswers.Read(endpoint, """{"id":"abc","mail":null,"userPrincipalName":"ada@contoso.example"}""");
        Assert.Equal(new ServiceIdentity("abc", "ada@contoso.example", "ada@contoso.example"), upn);

        Assert.Equal("abc", IdentityAnswers.Read(endpoint, """{"id":"abc"}""")!.Address);
        Assert.Null(IdentityAnswers.Read(endpoint, """{"mail":"x@example.com"}"""));
        Assert.Null(IdentityAnswers.Read(endpoint, "not json"));
    }

    [Fact]
    public void S9916_who_signed_in_to_google_is_its_subject_with_the_old_prefix()
    {
        var endpoint = OAuthClients.Google.SignIn.Identity!;
        Assert.Equal(OAuthClients.Google.UserInfoUrl, endpoint.Url);

        var who = IdentityAnswers.Read(endpoint, """{"sub":"1234","email":"ada@example.com","name":"Ada"}""");
        Assert.Equal(new ServiceIdentity(AccountIds.Google("1234"), "ada@example.com", "Ada"), who);
        Assert.Equal(new ServiceIdentity("google:1234", "1234", "1234"), IdentityAnswers.Read(endpoint, """{"sub":"1234"}"""));
    }
}
