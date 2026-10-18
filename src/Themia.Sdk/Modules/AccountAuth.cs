namespace Themia.Core.Accounts;

/// <summary>How an account proves itself on every request.</summary>
public enum AccountAuth
{
    /// <summary>A short-lived bearer token renewed from a refresh token (Microsoft, Google).</summary>
    OAuthRefresh,

    /// <summary>A user name and password sent with each connection (IMAP, CalDAV, CardDAV).</summary>
    Password,

    /// <summary>
    /// [S-9919] A token without a refresh: pasted by the user, or a grant that never expires. Sent as
    /// its sign-in member says ([S-9911]); a 401 means sign in again, there is nothing to renew.
    /// </summary>
    Token,
}

/// <summary>[S-9919] Which of its provider's sign-in members an account was signed in with.</summary>
public enum SignInKind
{
    /// <summary>The browser flow (<c>OAuthPkceLoopback</c>).</summary>
    Browser,

    /// <summary>The device code (<c>OAuthDeviceCode</c>).</summary>
    DeviceCode,

    /// <summary>A pasted token (<c>PastedToken</c>).</summary>
    PastedToken,
}
