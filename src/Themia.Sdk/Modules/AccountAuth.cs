namespace Themia.Core.Accounts;

/// <summary>How an account proves itself on every request.</summary>
public enum AccountAuth
{
    /// <summary>A short-lived bearer token renewed from a refresh token (Microsoft, Google).</summary>
    OAuthRefresh,

    /// <summary>A user name and password sent with each connection (IMAP, CalDAV, CardDAV).</summary>
    Password,
}
