using System.Text.RegularExpressions;

namespace Themia.Core.Diagnostics;

/// <summary>
/// [S-983] Takes secrets and addresses out of a log line before any sink sees it.
/// <para>
/// Applied once, centrally, in <see cref="Log"/>, rather than trusted to every call site: a log
/// line is assembled from whatever was at hand — an account id that happens to be
/// <c>imap:jane@example.com@imap.example.com</c>, an exception message that quotes a request URL
/// with <c>?code=</c> in it — and the person writing <c>Log.Warn</c> is not thinking about what the
/// interpolated value can contain on someone else's machine. The files are user-scoped but get
/// attached to issues, and errors are mirrored into the machine-wide Application event log.
/// </para>
/// <para>
/// What goes: bearer/basic credentials, secret-named parameters (<c>access_token=</c>,
/// <c>password:</c> …), OAuth query parameters, JWTs, well-known token prefixes (GitHub, Anthropic,
/// Slack, Google), long opaque mixed-case strings, the user-info part of a URL, and the local part
/// of an email address (kept: its first character and the domain, which is what tells a support
/// reader whether the account is Outlook or Gmail).
/// </para>
/// </summary>
public static partial class LogRedactor
{
    /// <summary>What a removed secret is replaced with.</summary>
    public const string Mask = "[redacted]";

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        // Order matters: user info before addresses (user:pw@host looks like an address), JWTs and
        // known prefixes before the generic opaque rule, and parameters before addresses so a
        // "password=a@b.c" loses the whole value rather than keeping its domain.
        var result = UrlUserInfo().Replace(text, "://" + Mask + "@");
        result = AuthScheme().Replace(result, m => m.Groups[1].Value + " " + Mask);
        result = SecretParameter().Replace(result, m => m.Groups[1].Value + m.Groups[2].Value + Mask);
        result = OAuthQueryParameter().Replace(result, m => m.Groups[1].Value + Mask);
        result = Jwt().Replace(result, Mask);
        result = KnownToken().Replace(result, Mask);
        result = Opaque().Replace(result, m => LooksOpaque(m.Value) ? Mask : m.Value);
        result = Email().Replace(result, m => m.Groups[1].Value + "***@" + m.Groups[2].Value);
        return result;
    }

    /// <summary>
    /// A long run counts as a secret only when it mixes upper case, lower case and digits, the
    /// shape of base64 key material. Hex digests, version strings and long lower-case words
    /// (paths are split on separators anyway) stay readable.
    /// </summary>
    private static bool LooksOpaque(string run)
    {
        bool upper = false, lower = false, digit = false;
        foreach (var c in run)
        {
            upper |= char.IsAsciiLetterUpper(c);
            lower |= char.IsAsciiLetterLower(c);
            digit |= char.IsAsciiDigit(c);
        }

        return upper && lower && digit;
    }

    /// <summary><c>scheme://user:password@host</c> → the user info goes.</summary>
    [GeneratedRegex(@"://[^/\s:@]+:[^/\s@]+@")]
    private static partial Regex UrlUserInfo();

    /// <summary><c>Bearer abc…</c>, <c>Basic abc…</c>: the credential after the scheme goes.</summary>
    [GeneratedRegex(@"\b(Bearer|Basic)\s+[A-Za-z0-9\-._~+/]{8,}=*", RegexOptions.IgnoreCase)]
    private static partial Regex AuthScheme();

    /// <summary>
    /// A parameter whose name says it is a secret, in <c>name=value</c>, <c>name: value</c> or JSON
    /// <c>"name":"value"</c> form. Names are deliberately unambiguous; "code" and "state" are
    /// only masked in a query string (<see cref="OAuthQueryParameter"/>), since "exit code: 5" is
    /// not a secret.
    /// </summary>
    [GeneratedRegex(
        @"\b((?:access|refresh|id)_?token|client_?secret|password|passwd|pwd|api_?key|apikey|secret|private_?key|authorization|cookie|set-cookie|x-api-key)(""?\s*[=:]\s*""?)[^\s&""',;]+",
        RegexOptions.IgnoreCase)]
    private static partial Regex SecretParameter();

    /// <summary>OAuth and signing parameters in a query string or fragment.</summary>
    [GeneratedRegex(
        @"([?&#](?:code|state|token|code_verifier|code_challenge|device_code|user_code|sig|signature|key|assertion)=)[^&#\s""']+",
        RegexOptions.IgnoreCase)]
    private static partial Regex OAuthQueryParameter();

    /// <summary>A JSON Web Token: three base64url parts, the first of which starts with <c>{"</c> (eyJ).</summary>
    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]*")]
    private static partial Regex Jwt();

    /// <summary>
    /// Tokens whose prefix gives them away even when short: GitHub (<c>ghp_</c>, <c>gho_</c>,
    /// <c>ghu_</c>, <c>ghs_</c>, <c>ghr_</c>, <c>github_pat_</c>), Anthropic (<c>sk-ant-</c>),
    /// Slack (<c>xox?-</c>), Google access tokens (<c>ya29.</c>), Google refresh tokens (<c>1//</c>).
    /// </summary>
    [GeneratedRegex(@"\b(?:gh[pousr]_[A-Za-z0-9]{16,}|github_pat_[A-Za-z0-9_]{16,}|sk-ant-[A-Za-z0-9_-]{8,}|xox[abprs]-[A-Za-z0-9-]{8,}|ya29\.[A-Za-z0-9_-]{8,})|(?<![\w/])1//[A-Za-z0-9_-]{16,}")]
    private static partial Regex KnownToken();

    /// <summary>
    /// A run of 40 or more base64 / base64url characters (longer than anything a line legitimately
    /// carries in one piece, shorter than any OAuth token); <see cref="LooksOpaque"/> decides.
    /// <c>/</c> and <c>.</c> are left out of the class on purpose: with them, every long URL path
    /// and dotted host name would read as one opaque run.
    /// </summary>
    [GeneratedRegex(@"[A-Za-z0-9+_\-=!*$]{40,}")]
    private static partial Regex Opaque();

    /// <summary>An email address: group 1 is the first character kept, group 2 the domain.</summary>
    [GeneratedRegex(@"\b([A-Za-z0-9])[A-Za-z0-9._%+\-]*@([A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?)*\.[A-Za-z]{2,})")]
    private static partial Regex Email();
}
