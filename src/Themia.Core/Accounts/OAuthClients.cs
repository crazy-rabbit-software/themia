using System.Globalization;


namespace Themia.Core.Accounts;

/// <summary>
/// The OAuth clients Themia signs in with, and the addresses of their endpoints.
/// <para>
/// Every value here is public by design. A desktop app cannot keep a secret, which is why each flow
/// is one that does not need one: PKCE (Microsoft, Google).
/// The one exception, Google's "Desktop" client secret, is not here — Google insists on it even with
/// PKCE, but it stays out of the public source and is compiled in from the build environment
/// (see <c>BuildSecrets</c> in the app).
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

        /// <summary>
        /// [S-1780] The loopback redirect. Microsoft ignores the port of a <c>localhost</c> redirect for a
        /// "Mobile and desktop applications" registration of <c>http://localhost</c>, but it does not
        /// match <c>127.0.0.1</c> against it, so this one names <c>localhost</c> (the listener answers
        /// on both loopback addresses, [S-1784]).
        /// </summary>
        public static string RedirectUri(int port) => $"http://localhost:{port.ToString(CultureInfo.InvariantCulture)}/";

        /// <summary>
        /// [S-1780] The sign-in page. <c>prompt=select_account</c> lets someone with several Microsoft
        /// accounts pick one instead of being signed in silently as whoever the browser remembers.
        /// </summary>
        public static Uri AuthorizeUrl(string redirectUri, string state, string challenge) => Build(
            $"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/authorize",
            ("response_type", "code"),
            ("client_id", ClientId),
            ("redirect_uri", redirectUri),
            ("response_mode", "query"),
            ("scope", Scope),
            ("state", state),
            ("code_challenge", challenge),
            ("code_challenge_method", "S256"),
            ("prompt", "select_account"));

        /// <summary>Who signed in: the Graph user id is the account id.</summary>
        public static readonly Uri MeUrl = new("https://graph.microsoft.com/v1.0/me?$select=id,mail,displayName,userPrincipalName");

        public static readonly Uri GraphRoot = new("https://graph.microsoft.com/");
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
        /// [S-527] The loopback redirect: the literal address Google recommends for desktop clients,
        /// so a browser that resolves <c>localhost</c> to ::1 first cannot miss it.
        /// </summary>
        public static string RedirectUri(int port) => "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The consent page. <c>access_type=offline</c> and <c>prompt=consent</c> together are what make
        /// Google hand out a refresh token every time, not only on the very first consent.
        /// </summary>
        public static Uri AuthorizeUrl(string redirectUri, string state, string challenge) => Build(
            "https://accounts.google.com/o/oauth2/v2/auth",
            ("response_type", "code"),
            ("client_id", ClientId),
            ("redirect_uri", redirectUri),
            ("scope", Scope),
            ("state", state),
            ("code_challenge", challenge),
            ("code_challenge_method", "S256"),
            ("access_type", "offline"),
            ("prompt", "consent"));
    }

    private static Uri Build(string root, params (string Key, string Value)[] query)
        => new(root + "?" + string.Join('&', query.Select(pair =>
            string.Create(CultureInfo.InvariantCulture, $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))));
}
