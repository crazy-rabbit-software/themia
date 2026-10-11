using System.Net;
using Themia.Core.Accounts;
using Themia.Core.Net;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3840]..[S-3843] The local network without a switch: only the host of an address the user typed
/// reaches the LAN, and only when that address names the LAN itself.
/// </summary>
public sealed class LocalGrantTests
{
    private static IPAddress Ip(string text) => IPAddress.Parse(text);

    private static GuardVerdict Judge(string host, string[]? resolved, LocalGrant? grant, bool proxied = false)
        => NetworkGuard.Judge(host, resolved?.Select(Ip).ToList(), proxied, grant, NetReach.Internet).Verdict;

    // ---- [S-3840] a typed LAN address is allowed ---------------------------------------------

    [Theory]
    [InlineData("http://192.168.1.20:8080/feed.xml")]
    [InlineData("http://10.0.0.5/cam.mjpg")]
    [InlineData("http://127.0.0.1:8085/")]
    [InlineData("http://100.101.102.103/")]   // carrier-grade NAT (Tailscale)
    [InlineData("http://[fd00::20]/feed")]     // unique-local
    [InlineData("http://[::1]:8080/")]
    public void S3840_a_typed_lan_literal_is_allowed(string url)
    {
        var uri = new Uri(url);
        var grant = LocalGrants.ForTyped(uri);

        Assert.NotNull(grant);
        Assert.Equal(GuardVerdict.Allowed, Judge(uri.IdnHost, null, grant));
    }

    [Theory]
    [InlineData("http://nas.local/rss", "192.168.1.20")]
    [InlineData("http://nas/rss", "192.168.1.20")]
    [InlineData("http://NAS.Lan./rss", "10.1.2.3")]
    [InlineData("http://media.home.arpa/rss", "fd12::5")]
    [InlineData("http://localhost:3000/", "127.0.0.1")]
    public void S3841_a_typed_local_name_reaches_the_lan_address_it_resolves_to(string url, string address)
    {
        var uri = new Uri(url);
        var grant = LocalGrants.ForTyped(uri);

        Assert.NotNull(grant);
        Assert.Equal(GuardVerdict.Allowed, Judge(uri.IdnHost, [address], grant));
    }

    [Fact]
    public void S3840_without_a_grant_the_lan_stays_closed()
    {
        Assert.Equal(GuardVerdict.Blocked, Judge("192.168.1.20", null, null));
        Assert.Equal(GuardVerdict.Blocked, Judge("nas.local", ["192.168.1.20"], null));
    }

    [Fact]
    public void S3840_a_public_address_needs_no_grant()
    {
        Assert.Null(LocalGrants.ForTyped(new Uri("https://8.8.8.8/")));
        Assert.Null(LocalGrants.ForTyped(new Uri("https://feeds.example.com/rss")));
        Assert.Equal(GuardVerdict.Allowed, Judge("feeds.example.com", ["93.184.216.34"], null));
    }

    // ---- [S-3840] bound to its host: redirects and content-derived addresses -----------------

    [Fact]
    public void S3840_a_redirect_from_a_public_host_to_a_private_one_is_blocked()
    {
        // The feed the user typed is public, so it earned no grant; where it redirects is the host's word.
        var typed = new Uri("https://feeds.example.com/rss");
        var grant = LocalGrants.ForTyped(typed);
        var next = new Uri("http://192.168.1.1/admin");

        Assert.False(RedirectRules.MayFollow(typed, next));
        Assert.False(LocalGrants.Covers(grant, next.IdnHost));
        Assert.Equal(GuardVerdict.Blocked, Judge(next.IdnHost, null, grant));
    }

    [Fact]
    public void S3840_a_redirect_from_a_typed_lan_host_to_another_lan_host_carries_no_grant()
    {
        var grant = LocalGrants.ForTyped(new Uri("http://192.168.1.20/feed"));

        Assert.False(LocalGrants.Covers(grant, "192.168.1.21"));
        Assert.Equal(GuardVerdict.Blocked, Judge("192.168.1.21", null, grant));
        Assert.Equal(GuardVerdict.Blocked, Judge("router.local", ["192.168.1.1"], grant));
    }

    [Fact]
    public void S3840_a_redirect_within_the_typed_host_keeps_its_grant()
    {
        var grant = LocalGrants.ForTyped(new Uri("http://nas.local/feed"));
        var next = new Uri("https://NAS.local:8443/feed.xml");

        Assert.True(RedirectRules.MayFollow(new Uri("http://nas.local/feed"), next));
        Assert.True(LocalGrants.Covers(grant, next.IdnHost));
        Assert.Equal(GuardVerdict.Allowed, Judge(next.IdnHost, ["192.168.1.20"], grant));
    }

    [Fact]
    public void S3840_an_address_found_in_fetched_content_carries_no_grant()
    {
        // A feed item's link or picture is somebody else's text: the caller asks with no grant, so a
        // private address in it is refused even when the feed itself was typed on the LAN.
        Assert.Equal(GuardVerdict.Blocked, Judge("10.0.0.1", null, grant: null));
        Assert.Equal(GuardVerdict.Blocked, Judge("printer.lan", ["192.168.1.9"], grant: null));
    }

    // ---- [S-3841] DNS rebinding: a typed public name stays a public name -----------------------

    [Theory]
    [InlineData("https://rss.example.com/feed", "192.168.1.20")]
    [InlineData("https://rebind.attacker.example/", "127.0.0.1")]
    [InlineData("https://nas.mydomain.com/", "10.0.0.5")]
    public void S3841_a_typed_public_name_that_resolves_to_a_private_address_stays_blocked(string url, string address)
    {
        var uri = new Uri(url);
        var grant = LocalGrants.ForTyped(uri);

        Assert.Null(grant);
        Assert.Equal(GuardVerdict.Blocked, Judge(uri.IdnHost, [address], grant));
    }

    [Fact]
    public void S3841_a_grant_is_for_its_host_only_even_when_handed_to_another()
    {
        var stolen = new LocalGrant("nas.local");
        Assert.Equal(GuardVerdict.Blocked, Judge("rebind.example", ["192.168.1.20"], stolen));
    }

    [Theory]
    [InlineData("nas", true)]
    [InlineData("localhost", true)]
    [InlineData("printer.local", true)]
    [InlineData("box.lan", true)]
    [InlineData("pi.home.arpa", true)]
    [InlineData("git.internal", true)]
    [InlineData("example.com", false)]
    [InlineData("local.example.com", false)]
    [InlineData("example.localhost.com", false)]
    [InlineData("192.168.1.1", false)]
    public void S3841_local_names(string host, bool local)
        => Assert.Equal(local, LocalGrants.IsLocalName(host));

    // ---- [S-3842] what a grant can never open ---------------------------------------------------

    [Theory]
    [InlineData("169.254.169.254")]   // cloud metadata
    [InlineData("169.254.1.1")]
    [InlineData("[fe80::1]")]
    [InlineData("0.0.0.0")]
    [InlineData("240.0.0.1")]
    [InlineData("192.0.2.1")]         // documentation
    public void S3842_a_typed_link_local_or_reserved_literal_earns_no_grant(string host)
    {
        Assert.Null(LocalGrants.ForTypedHost(host));
        Assert.Equal(GuardVerdict.Blocked, Judge(host, null, new LocalGrant(host.Trim('[', ']'))));
    }

    [Fact]
    public void S3842_a_granted_name_that_also_resolves_to_link_local_is_blocked()
        => Assert.Equal(GuardVerdict.Blocked,
            Judge("nas.local", ["192.168.1.20", "169.254.169.254"], LocalGrants.ForTypedHost("nas.local")));

    [Fact]
    public void S3842_a_proxy_is_still_no_bypass_for_an_ungranted_literal()
    {
        Assert.Equal(GuardVerdict.Blocked, Judge("127.0.0.1", null, null, proxied: true));
        Assert.Equal(GuardVerdict.Allowed, Judge("127.0.0.1", null, new LocalGrant("127.0.0.1"), proxied: true));
    }

    // ---- [S-3843] account servers --------------------------------------------------------------

    [Fact]
    public void S3843_a_typed_imap_or_dav_server_on_the_lan_earns_a_grant()
    {
        Assert.NotNull(LocalGrants.ForTypedHost("192.168.1.10"));
        Assert.NotNull(LocalGrants.ForTyped(new Uri("https://nextcloud.lan/remote.php/dav")));
        Assert.Null(LocalGrants.ForTypedHost("imap.example.com"));
        Assert.Null(LocalGrants.ForTypedHost(" "));
    }
}
