using System.Net.Sockets;
using Themia.Core.Accounts;
using Themia.Core.Mail;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-528] [S-553] One verdict on an IMAP session for the sign-in and the mail sync: the sign-in ran a client of its own
/// that judged the same server differently (a BYE was "refused", an offline PC "unreachable", a NO always "sign in").
/// </summary>
public sealed class ImapVerdictTests
{
    private const string Host = "imap.example.org";

    [Fact]
    public void S553_a_bye_greeting_is_the_servers_trouble_never_a_refusal_of_the_account()
    {
        var bye = ImapVerdict.Of(ImapFailure.Bye, Host, 993);

        Assert.Equal(AccountError.ServerError, bye.Error);
        Assert.Contains(Host, bye.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[AUTHENTICATIONFAILED] Invalid credentials", AccountError.SignInRequired)]
    [InlineData("", AccountError.SignInRequired)]
    [InlineData("[UNAVAILABLE] Try again later", AccountError.ServerError)]
    [InlineData("[LIMIT] Too many connections", AccountError.ServerError)]
    public void S528_a_refused_login_asks_for_a_sign_in_only_when_the_credentials_are_wrong(string text, AccountError expected)
    {
        var refused = ImapVerdict.LoginRefused(text);

        Assert.Equal(expected, refused.Error);
        if (text.Length > 0)
        {
            Assert.Contains(text, refused.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(SocketError.HostNotFound, false, AccountError.Offline)]
    [InlineData(SocketError.NetworkUnreachable, false, AccountError.Offline)]
    [InlineData(SocketError.HostNotFound, true, AccountError.Unreachable)]
    [InlineData(SocketError.ConnectionRefused, false, AccountError.Unreachable)]
    [InlineData(SocketError.TimedOut, true, AccountError.Unreachable)]
    public void S553_a_socket_that_failed_for_want_of_a_network_is_offline_else_unreachable(SocketError socket, bool networkAvailable, AccountError expected)
    {
        var failed = ImapVerdict.Of(ImapFailure.Socket, Host, 993, socket: socket, networkAvailable: networkAvailable);

        Assert.Equal(expected, failed.Error);
        Assert.Contains($"{Host}:993", failed.Message, StringComparison.Ordinal);
        Assert.Contains(socket.ToString(), failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void S528_a_failed_tls_handshake_is_unreachable_and_names_the_host_and_port()
    {
        var inner = new System.Security.Authentication.AuthenticationException("handshake");
        var tls = ImapVerdict.Of(ImapFailure.Tls, Host, 143, inner: inner);

        Assert.Equal(AccountError.Unreachable, tls.Error);
        Assert.Contains($"{Host}:143", tls.Message, StringComparison.Ordinal);
        Assert.Same(inner, tls.InnerException);
    }

    [Theory]
    [InlineData(ImapFailure.NotImap, AccountError.BadResponse)]
    [InlineData(ImapFailure.TimedOut, AccountError.TimedOut)]
    [InlineData(ImapFailure.NetRefused, AccountError.Refused)]
    [InlineData(ImapFailure.Proxy, AccountError.Unreachable)]
    [InlineData(ImapFailure.Dropped, AccountError.Unreachable)]
    public void S553_every_other_failure_has_one_meaning(ImapFailure failure, AccountError expected)
        => Assert.Equal(expected, ImapVerdict.Of(failure, Host, 993, detail: "the guard's words").Error);

    [Fact]
    public void S553_no_failure_but_a_refused_login_asks_for_a_sign_in()
    {
        foreach (var failure in Enum.GetValues<ImapFailure>())
        {
            Assert.NotEqual(AccountError.SignInRequired, ImapVerdict.Of(failure, Host, 993, networkAvailable: false).Error);
        }
    }
}
