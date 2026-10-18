using Themia.Core.Widgets;

namespace Themia.Core.Accounts;

/// <summary>
/// The one exception account requests throw. Typed, so a caller switches on <see cref="Error"/>
/// rather than on message text, and carries the provider's own words when it gave any.
/// </summary>
public sealed class AccountException : Exception
{
    public AccountException(AccountError error, string message, int httpStatus = 0, Exception? inner = null)
        : base(message, inner)
    {
        Error = error;
        HttpStatus = httpStatus;
    }

    public AccountException()
        : this(AccountError.BadResponse, "account request failed")
    {
    }

    public AccountException(string message)
        : this(AccountError.BadResponse, message)
    {
    }

    public AccountException(string message, Exception inner)
        : this(AccountError.BadResponse, message, 0, inner)
    {
    }

    public AccountError Error { get; }

    /// <summary>The HTTP status, or 0 when the failure was not an HTTP answer.</summary>
    public int HttpStatus { get; }

    /// <summary>
    /// The provider's own words for an HTTP refusal (<see cref="ErrorBody.Message"/>), without the
    /// host and status <see cref="Exception.Message"/> wraps them in; null when it gave none. For a
    /// widget that maps a status to its own sentence and quotes the provider's reason.
    /// </summary>
    public string? ProviderMessage { get; init; }

    /// <summary>
    /// [S-625] For <see cref="AccountError.RateLimited"/>: when asking again is worth it, as the
    /// provider's <c>Retry-After</c> or rate-limit reset said. Null when it named no time.
    /// </summary>
    public DateTimeOffset? RetryAt { get; init; }

    /// <summary>
    /// [S-9900] The matching widget fetch state, so a widget built on <see cref="FetchState"/> shows the
    /// same sentence for an account failure as for any other network one. Nothing is folded away: a
    /// sign-in, a refusal and a missing item keep their own state; only a server failure and a body
    /// that did not parse share "unexpected", and an account that is gone is "not set up".
    /// </summary>
    public FetchStatus ToFetchStatus() => Error switch
    {
        AccountError.Offline => FetchStatus.Offline,
        AccountError.Unreachable => FetchStatus.Unreachable,
        AccountError.TimedOut => FetchStatus.TimedOut,
        AccountError.RateLimited => FetchStatus.RateLimited,
        AccountError.UnknownAccount => FetchStatus.NotConfigured,
        AccountError.SignInRequired => FetchStatus.SignInRequired,
        AccountError.Forbidden => FetchStatus.Forbidden,
        AccountError.NotFound => FetchStatus.NotFound,
        AccountError.Refused => FetchStatus.Refused,
        AccountError.ServerError or AccountError.BadResponse => FetchStatus.BadResponse,
        _ => FetchStatus.BadResponse,
    };

    /// <summary>The error a non-success HTTP status means.</summary>
    public static AccountError FromStatus(int status) => status switch
    {
        401 => AccountError.SignInRequired,
        403 => AccountError.Forbidden,
        404 or 410 => AccountError.NotFound,
        429 => AccountError.RateLimited,
        >= 500 and < 600 => AccountError.ServerError,
        _ => AccountError.BadResponse,
    };
}
