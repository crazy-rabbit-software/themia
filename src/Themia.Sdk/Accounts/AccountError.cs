namespace Themia.Core.Accounts;

/// <summary>What went wrong with an account request, named so a widget can say the right thing.</summary>
public enum AccountError
{
    /// <summary>No account with that id (signed out meanwhile, or a widget config naming a deleted one).</summary>
    UnknownAccount,

    /// <summary>
    /// Only the user can mend this: the refresh token was revoked, the password changed, or the secret
    /// cannot be read on this PC. Retrying will not help; the account is kept and says "sign in again".
    /// </summary>
    SignInRequired,

    /// <summary>Signed in, but this account may not do this (HTTP 403: a missing scope, a policy).</summary>
    Forbidden,

    /// <summary>HTTP 404: the item or collection is not there.</summary>
    NotFound,

    /// <summary>HTTP 429, or a 503 that kept coming after the retries.</summary>
    RateLimited,

    /// <summary>The provider failed (5xx).</summary>
    ServerError,

    /// <summary>Any other refusal, or a body that does not parse.</summary>
    BadResponse,

    /// <summary>This PC has no network.</summary>
    Offline,

    /// <summary>The network is up but the host did not answer.</summary>
    Unreachable,

    /// <summary>The request ran out of time.</summary>
    TimedOut,

    /// <summary>The request was refused before it was sent (a non-https address, a redirect off-site).</summary>
    Refused,
}
