using Themia.Core.Model;
using Themia.Core.SystemData;
using Xunit;

namespace Themia.Widget.NetworkFacts.Tests;

/// <summary>[S-2417] [S-11020] The network facts: the public address is the user's pick, never a default.</summary>
public sealed class NetworkFactsTests
{
    [Fact]
    public void S2417_the_public_address_is_off_by_default()
    {
        Assert.False(new NetworkFactsConfig().ShowsPublicAddress);
        Assert.DoesNotContain(FeedKey.Create(SystemSource.PublicAddress), SystemInterests.For(new NetworkFactsConfig(), @"C:\").Feeds);
    }

    [Fact]
    public void S11020_a_new_one_shows_the_address_the_totals_and_the_link_speed()
        => Assert.Equal(["localAddress", "received", "sent", "linkSpeed"], new NetworkFactsConfig().Facts);
}
