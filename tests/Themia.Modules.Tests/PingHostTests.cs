using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;
using Themia.Core.Localization;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-1173] [S-3791] The ping meter's config under the root's rules (the clone keeps the host, a stored
/// host label is ignored): the config is the ping module's, so they run here. Moved from Core's
/// DiskPingTests, unchanged in what they assert.
/// </summary>
public sealed class PingHostTests
{

    [Fact]
    public void S1173_clone_keeps_the_host()
    {
        var copy = (PingConfig)new PingConfig { Host = "8.8.8.8" }.Clone();

        Assert.Equal("8.8.8.8", copy.Host);
    }

    [Fact]
    public void S3791_a_stored_host_label_is_ignored_and_the_host_is_the_name()
    {
        var config = (PingConfig)ConfigJson.Read("Ping", """{ "kind": "ping", "host": "8.8.8.8", "hostLabel": "DNS", "intervalSeconds": 300 }""");

        Assert.Equal("8.8.8.8", config.EffectiveHost);
        Assert.Empty(config.Repair());
    }
}
