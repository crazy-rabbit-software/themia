using System.Buffers.Binary;
using System.Net;
using System.Text;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.NetworkDetails.Tests;

/// <summary>[S-2417] The network details widget: the public address is the user's choice. Moved with the widget from Core's SystemDetailsTests, unchanged in what it asserts.</summary>
public sealed class NetworkDetailsTests
{
    [Fact]
    public void S2417_the_public_address_is_off_by_default()
        => Assert.False(new NetworkDetailsConfig().ShowPublicAddress);
}
