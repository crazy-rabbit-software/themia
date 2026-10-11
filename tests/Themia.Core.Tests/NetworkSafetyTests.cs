using System.Net;
using System.Text;
using Themia.Core.Accounts;
using Themia.Core.Net;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-960]..[S-969] Network safety: the address classes behind the one guard, the connect-time
/// verdicts, which connection is a proxy's, and the CONNECT tunnel an IMAP connection makes.
/// </summary>
public sealed class NetworkSafetyTests
{
    private static IPAddress Ip(string text) => IPAddress.Parse(text);

    // ---- [S-961] address classes, IPv4 carried inside IPv6 ------------------------------------

    [Theory]
    [InlineData("::ffff:127.0.0.1")]      // IPv4-mapped loopback
    [InlineData("::ffff:169.254.169.254")] // mapped cloud metadata
    [InlineData("::127.0.0.1")]           // IPv4-compatible (deprecated)
    [InlineData("::10.0.0.1")]
    [InlineData("64:ff9b::7f00:1")]       // NAT64 of 127.0.0.1
    [InlineData("64:ff9b::a00:1")]        // NAT64 of 10.0.0.1
    [InlineData("64:ff9b::a9fe:a9fe")]    // NAT64 of 169.254.169.254
    [InlineData("fec0::1")]               // site-local (deprecated)
    [InlineData("::")]
    [InlineData("::1")]
    public void S961_ipv6_that_carries_a_private_ipv4_or_is_private_is_blocked(string address)
        => Assert.True(NetworkGuard.IsBlocked(Ip(address)));

    [Theory]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("64:ff9b::808:808")]      // NAT64 of 8.8.8.8
    [InlineData("::8.8.8.8")]
    [InlineData("2606:4700::1111")]
    public void S961_ipv6_that_carries_a_public_ipv4_or_is_public_passes(string address)
        => Assert.False(NetworkGuard.IsBlocked(Ip(address)));

    [Theory]
    [InlineData("::ffff:192.168.1.2", "192.168.1.2")]
    [InlineData("64:ff9b::c0a8:102", "192.168.1.2")]
    [InlineData("::192.168.1.2", "192.168.1.2")]
    public void S961_the_carried_ipv4_is_read_out(string v6, string v4)
        => Assert.Equal(Ip(v4), NetworkGuard.EmbeddedV4(Ip(v6)));

    [Theory]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("2606:4700::1111")]
    [InlineData("64:ff9b:1::a00:1")]      // a local-use NAT64 prefix, not the well-known one
    [InlineData("8.8.8.8")]
    public void S961_an_address_that_carries_no_ipv4_has_none(string address)
        => Assert.Null(NetworkGuard.EmbeddedV4(Ip(address)));

    // ---- [S-962] the connect-time verdict ---------------------------------------------------

    [Fact]
    public void S962_a_name_is_judged_on_every_address_it_resolved_to_at_connect()
    {
        // The addresses judged are the ones connected to: one private address among public ones
        // (the shape of a rebinding answer) refuses the whole connection.
        var (verdict, culprit) = NetworkGuard.Judge("rebind.example", [Ip("93.184.216.34"), Ip("::ffff:127.0.0.1")],
            proxied: false, grant: null, NetReach.Internet);

        Assert.Equal(GuardVerdict.Blocked, verdict);
        Assert.Equal(Ip("::ffff:127.0.0.1"), culprit);
    }

    [Fact]
    public void S962_an_address_literal_is_judged_as_written_whatever_a_resolver_says()
        => Assert.Equal(GuardVerdict.Blocked,
            NetworkGuard.Judge("127.0.0.1", [Ip("93.184.216.34")], proxied: false, grant: null, NetReach.Internet).Verdict);

    [Fact]
    public void S962_S3840_a_nas_is_admitted_by_the_grant_its_typed_address_earns()
    {
        Assert.Equal(GuardVerdict.Blocked,
            NetworkGuard.Judge("nas.local", [Ip("192.168.1.20")], proxied: false, grant: null, NetReach.Internet).Verdict);
        Assert.Equal(GuardVerdict.Allowed,
            NetworkGuard.Judge("nas.local", [Ip("192.168.1.20")], proxied: false, LocalGrants.ForTypedHost("nas.local"), NetReach.Internet).Verdict);
    }

    [Fact]
    public void S962_a_name_that_resolved_to_nothing_is_unresolvable_when_direct()
        => Assert.Equal(GuardVerdict.Unresolvable,
            NetworkGuard.Judge("empty.example", [], proxied: false, grant: null, NetReach.Internet).Verdict);

    // ---- [S-967] this machine only ----------------------------------------------------------

    [Fact]
    public void S967_this_machine_admits_loopback_only_whatever_grant_came_along()
    {
        Assert.Equal(GuardVerdict.Allowed,
            NetworkGuard.Judge("localhost", [Ip("::1"), Ip("127.0.0.1")], proxied: false, grant: null, NetReach.ThisMachine).Verdict);
        Assert.Equal(GuardVerdict.Allowed,
            NetworkGuard.Judge("::ffff:127.0.0.1", null, proxied: false, grant: null, NetReach.ThisMachine).Verdict);

        Assert.Equal(GuardVerdict.Blocked,
            NetworkGuard.Judge("localhost", [Ip("127.0.0.1"), Ip("10.0.0.5")], proxied: false, new LocalGrant("localhost"), NetReach.ThisMachine).Verdict);
        Assert.Equal(GuardVerdict.Blocked,
            NetworkGuard.Judge("192.168.1.1", null, proxied: false, new LocalGrant("192.168.1.1"), NetReach.ThisMachine).Verdict);
        Assert.Equal(GuardVerdict.Blocked,
            NetworkGuard.Judge("8.8.8.8", null, proxied: false, grant: null, NetReach.ThisMachine).Verdict);
    }

    [Fact]
    public void S967_this_machine_is_never_relaxed_by_a_proxy()
        => Assert.Equal(GuardVerdict.Unresolvable,
            NetworkGuard.Judge("monitor.example", null, proxied: true, grant: null, NetReach.ThisMachine).Verdict);

    // ---- [S-963] which connection is the proxy's --------------------------------------------

    [Theory]
    [InlineData("feed.example", 443, "feed.example", 443, false)]
    [InlineData("Feed.Example", 443, "feed.example.", 443, false)]
    [InlineData("[::1]", 8085, "::1", 8085, false)]
    [InlineData("::ffff:10.0.0.1", 80, "::ffff:10.0.0.1", 80, false)]
    [InlineData("feed.example", 443, "10.0.0.8", 8080, true)]
    [InlineData("feed.example", 443, "proxy.corp", 443, true)]
    [InlineData("feed.example", 443, "feed.example", 8443, true)]
    public void S963_a_connection_to_another_endpoint_than_the_requests_is_the_proxys(
        string requestHost, int requestPort, string endpointHost, int endpointPort, bool expected)
        => Assert.Equal(expected, ConnectRules.IsProxyHop(requestHost, requestPort, endpointHost, endpointPort));

    [Fact]
    public void S963_the_system_proxys_answer_is_read_as_a_route()
    {
        var destination = new Uri("https://imap.example:993/");

        Assert.Equal(ProxyRoute.Direct, ConnectRules.Classify(null, destination));
        Assert.Equal(ProxyRoute.Direct, ConnectRules.Classify(destination, destination));
        Assert.Equal(ProxyRoute.Http, ConnectRules.Classify(new Uri("http://10.0.0.8:8080"), destination));
        Assert.Equal(ProxyRoute.Unsupported, ConnectRules.Classify(new Uri("socks5://10.0.0.8:1080"), destination));
    }

    // ---- [S-964] the CONNECT tunnel ---------------------------------------------------------

    [Theory]
    [InlineData("imap.example", 993, "https://imap.example:993/")]
    [InlineData("::1", 993, "https://[::1]:993/")]
    [InlineData("[2001:db8::5]", 143, "https://[2001:db8::5]:143/")]
    public void S964_a_mail_server_is_put_to_the_proxy_as_an_https_address(string host, int port, string expected)
        => Assert.Equal(new Uri(expected), ConnectRules.TunnelQuestion(host, port));

    [Theory]
    [InlineData("", 993)]
    [InlineData("imap.example", 0)]
    [InlineData("imap.example", 70000)]
    [InlineData("bad host/name", 993)]
    public void S964_no_question_for_an_address_no_uri_can_hold(string host, int port)
        => Assert.Null(ConnectRules.TunnelQuestion(host, port));

    [Fact]
    public void S964_the_request_names_the_server_twice_and_ends_with_a_blank_line()
    {
        Assert.Equal("CONNECT imap.example:993 HTTP/1.1\r\nHost: imap.example:993\r\nUser-Agent: Themia/1.0\r\n\r\n",
            ProxyTunnel.Request("imap.example", 993, "Themia/1.0"));
        Assert.Equal("CONNECT [::1]:993 HTTP/1.1\r\nHost: [::1]:993\r\n\r\n",
            ProxyTunnel.Request("::1", 993, string.Empty));
    }

    [Theory]
    [InlineData("HTTP/1.1 200 Connection established\r\n\r\n", true)]
    [InlineData("HTTP/1.0 200 OK\n\n", true)]
    [InlineData("HTTP/1.1 200 OK\r\n", false)]
    [InlineData("HTTP/1.1 200 OK\r\nVia: x\r\n", false)]
    [InlineData("", false)]
    public void S964_the_reply_is_read_to_the_blank_line_and_no_further(string read, bool end)
        => Assert.Equal(end, ProxyTunnel.IsHeaderEnd(Encoding.ASCII.GetBytes(read)));

    [Theory]
    [InlineData("HTTP/1.1 200 Connection established\r\nProxy-Agent: x\r\n\r\n", TunnelReply.Open, 200)]
    [InlineData("HTTP/1.0 200 OK\r\n\r\n", TunnelReply.Open, 200)]
    [InlineData("HTTP/1.1 407 Proxy Authentication Required\r\n\r\n", TunnelReply.AuthenticationRequired, 407)]
    [InlineData("HTTP/1.1 403 Forbidden\r\n\r\n", TunnelReply.Refused, 403)]
    [InlineData("HTTP/1.1 502 Bad Gateway\r\n\r\n", TunnelReply.Refused, 502)]
    [InlineData("* OK IMAP4rev1 ready\r\n\r\n", TunnelReply.Malformed, 0)]
    [InlineData("HTTP/1.1 2000 Odd\r\n\r\n", TunnelReply.Malformed, 0)]
    [InlineData("", TunnelReply.Malformed, 0)]
    public void S964_the_status_line_decides_the_tunnel(string headers, TunnelReply reply, int status)
    {
        var parsed = ProxyTunnel.Parse(headers);

        Assert.Equal(reply, parsed.Reply);
        Assert.Equal(status, parsed.Status);
    }

    [Fact]
    public void S964_a_refusal_keeps_the_proxys_own_status_line_for_the_message()
        => Assert.Equal("HTTP/1.1 403 CONNECT to port 993 not allowed",
            ProxyTunnel.Parse("HTTP/1.1 403 CONNECT to port 993 not allowed\r\n\r\n").StatusLine);

    // ---- [S-966] one table of address classes for redirects too -----------------------------

    [Theory]
    [InlineData("https://100.64.0.1/", true)]          // CGNAT, which the old private-only table missed
    [InlineData("https://[::ffff:10.0.0.1]/", true)]
    [InlineData("https://[64:ff9b::a00:1]/", true)]
    [InlineData("https://198.51.100.7/", true)]        // documentation range
    [InlineData("https://a.localhost/", true)]
    [InlineData("https://93.184.216.34/", false)]
    [InlineData("https://feed.example/", false)]
    public void S966_redirect_locality_uses_the_guards_address_classes(string url, bool local)
        => Assert.Equal(local, RedirectRules.IsLocal(new Uri(url)));

    [Fact]
    public void S966_a_public_host_cannot_redirect_into_the_local_network()
        => Assert.False(RedirectRules.MayFollow(new Uri("https://feed.example/rss"), new Uri("https://100.64.0.1/admin")));
}
