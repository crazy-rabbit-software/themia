using Themia.Sdk.Services;

namespace Themia.Core.Accounts;

/// <summary>
/// The OAuth clients Themia signs in with, and the addresses of their endpoints.
/// <para>
/// Every value here is public by design. A desktop app cannot keep a secret, which is why each flow
/// is one that does not need one: PKCE (Microsoft, Google). The one exception, Google's "Desktop"
/// client secret, is not here: Google insists on it even with PKCE, but it stays out of the public
/// source and is compiled in from the build environment (<c>THEMIA_CLIENT_SECRETS</c>, [S-9917]).
/// Each provider's flow is its sign-in member, data the host's one browser path runs ([S-9910]).
/// </para>
/// </summary>
public static class OAuthClients
{
    /// <summary>
    /// Microsoft identity platform, v2 endpoints, multi-tenant, as a public client: browser, loopback
    /// redirect, PKCE, and no client secret ([S-1780]).
    /// </summary>
    public static class Microsoft
    {
        public const string ClientId = "6cf22c0f-103a-4f8a-ae35-178d62575897";

        public const string Tenant = "common";

        /// <summary>[S-509] The Microsoft Graph scopes the widgets need.</summary>
        public const string Scope = "User.Read Mail.ReadWrite Calendars.Read Contacts.Read Tasks.ReadWrite offline_access";

        public static readonly Uri TokenUrl = new($"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/token");

        /// <summary>Who signed in: the Graph user id is the account id.</summary>
        public static readonly Uri MeUrl = new("https://graph.microsoft.com/v1.0/me?$select=id,mail,displayName,userPrincipalName");

        public static readonly Uri GraphRoot = new("https://graph.microsoft.com/");

        /// <summary>
        /// [S-1780] [S-9910] The browser flow: a public client (no secret), the scopes sent to the
        /// token endpoint too, the account chosen each time (<c>prompt=select_account</c>, so someone
        /// with several Microsoft accounts picks one instead of being signed in silently as whoever
        /// the browser remembers), and the loopback redirect. Microsoft ignores the port of a
        /// <c>localhost</c> redirect for a "Mobile and desktop applications" registration of
        /// <c>http://localhost</c>, but it does not match <c>127.0.0.1</c> against it, so the redirect
        /// names <c>localhost</c> (the listener answers on both loopback addresses, [S-1784]).
        /// </summary>
        public static readonly OAuthPkceLoopback SignIn = new(
            AuthorizeUrl: new($"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/authorize"),
            TokenUrl: TokenUrl,
            ClientId: ClientId,
            Scopes: Scope.Split(' '),
            Separator: ScopeSeparator.Space,
            Loopback: new LoopbackRule(LoopbackHost.Localhost, "/"),
            ClientSecret: ClientSecretRule.None,
            Refresh: new RefreshRule(Rotates: true, ReuseGrace: TimeSpan.Zero),
            AuthorizeExtras: [new("response_mode", "query"), new("prompt", "select_account")],
            ScopeOnToken: true,
            Identity: new IdentityEndpoint(MeUrl, "id", ["mail", "userPrincipalName"], "displayName"));
    }

    /// <summary>Google's installed-app client: browser, loopback redirect, PKCE.</summary>
    public static class Google
    {
        public const string ClientId = "187748979097-url067h00d18pjbjgskddssdj0nrq04s.apps.googleusercontent.com";

        public const string Scope = "openid email profile "
            + "https://www.googleapis.com/auth/gmail.readonly "
            + "https://www.googleapis.com/auth/calendar.events.readonly "
            + "https://www.googleapis.com/auth/tasks "
            + "https://www.googleapis.com/auth/contacts.readonly";

        public static readonly Uri TokenUrl = new("https://oauth2.googleapis.com/token");

        public static readonly Uri UserInfoUrl = new("https://www.googleapis.com/oauth2/v3/userinfo");

        /// <summary>
        /// [S-527] [S-9910] The browser flow: the Desktop client's secret from the build ([S-536]), the
        /// literal loopback address Google recommends for desktop clients (so a browser that resolves
        /// <c>localhost</c> to ::1 first cannot miss it), and <c>access_type=offline</c> with
        /// <c>prompt=consent</c>, which together make Google hand out a refresh token every time, not
        /// only on the very first consent.
        /// </summary>
        public static readonly OAuthPkceLoopback SignIn = new(
            AuthorizeUrl: new("https://accounts.google.com/o/oauth2/v2/auth"),
            TokenUrl: TokenUrl,
            ClientId: ClientId,
            Scopes: Scope.Split(' '),
            Separator: ScopeSeparator.Space,
            Loopback: new LoopbackRule(LoopbackHost.Loopback4, string.Empty),
            ClientSecret: ClientSecretRule.BuildTime,
            Refresh: RefreshRule.Stable,
            AuthorizeExtras: [new("access_type", "offline"), new("prompt", "consent")],
            Identity: new IdentityEndpoint(UserInfoUrl, "sub", ["email"], "name", "google:"));
    }
}
