using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;
using Themia.Core.Localization;

namespace Themia.Modules.Tests;

/// <summary>[S-4670] The drive card's config under the host's drive rules (a root kept, anything else repaired): the config is the drive module's, the rule's tag the root's, so it runs here. Moved from Core's DriveRulesTests, unchanged in what it asserts.</summary>
public sealed class DriveHostTests
{

    [Fact]
    public void S4670_the_drive_config_keeps_a_root_and_repairs_anything_else()
    {
        var config = (DriveConfig)ConfigJson.Read("drive", """{ "kind": "drive", "drive": "d:" }""");
        Assert.Equal("D:\\", config.Drive);

        var bad = new DriveConfig { Drive = "C:\\Users" };
        Assert.NotEmpty(bad.Repair());
        Assert.Equal(string.Empty, bad.Drive);
    }
}
