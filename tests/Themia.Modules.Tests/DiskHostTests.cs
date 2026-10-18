using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-1160] [S-1163] [S-1822] The disk meter's config under the host's disk rules (the drive's root, the
/// history on by default, the scope's repair): the config is the disk module's, the rules' tags the
/// root's, so they run here. Moved from Core's DiskPingTests, unchanged in what they assert.
/// </summary>
public sealed class DiskHostTests
{

    [Fact]
    public void S1160_a_clean_config_repairs_to_nothing_and_clones_every_option()
    {
        var config = new DiskConfig { Drive = "D:\\", Scope = DiskScope.AllDrives };

        Assert.Empty(config.Repair());

        var copy = (DiskConfig)config.Clone();
        Assert.Equal("D:\\", copy.Drive);
        Assert.Equal(DiskScope.AllDrives, copy.Scope);
    }

    [Theory]
    [InlineData("d", "D:\\")]
    [InlineData("D:", "D:\\")]
    [InlineData("d:\\", "D:\\")]
    [InlineData("", "")]
    public void S1160_repair_normalises_the_drive_to_its_root(string drive, string expected)
    {
        var config = new DiskConfig { Drive = drive };

        Assert.Empty(config.Repair());
        Assert.Equal(expected, config.Drive);
    }

    [Theory]
    [InlineData("not a drive")]
    [InlineData("\\\\server\\share")]
    [InlineData("1:")]
    public void S1160_a_drive_that_is_not_a_drive_letter_is_dropped_loudly_for_the_system_drive(string drive)
    {
        var config = new DiskConfig { Drive = drive };

        Assert.Single(config.Repair());
        Assert.Equal(string.Empty, config.Drive);
    }

    [Fact]
    public void S1163_the_history_is_on_by_default_because_it_charts_reads_and_writes()
        => Assert.True(new DiskConfig().ShowGraph);

    [Fact]
    public void S1822_an_unknown_scope_is_repaired_loudly_to_one_drive()
    {
        var unknown = new DiskConfig { Scope = (DiskScope)9 };

        Assert.Single(unknown.Repair());
        Assert.Equal(DiskScope.OneDrive, unknown.Scope);
    }
}
