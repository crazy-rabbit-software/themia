using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.DiskHistory.Tests;

/// <summary>[S-11018] The disk history atom: headerless, its drive's series, its lines.</summary>
public sealed class DiskHistoryTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("diskHistory"));

    [Fact]
    public void S11018_it_starts_headerless_and_charts_the_drive_the_reading_reads()
    {
        Assert.True(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));
        Assert.Equal(MeterSizing.History, Kind.SizingOf());
        Assert.Equal(300, new DiskHistoryConfig().GraphSpanSeconds);
        Assert.Equal(
            SystemInterests.For(new DiskConfig { Drive = "D:\\" }, @"C:\").Series,
            SystemInterests.For(new DiskHistoryConfig { Drive = "D:\\" }, @"C:\").Series);
        Assert.Equal(
            SystemInterests.For(new DiskConfig { Scope = DiskScope.AllDrives }, @"C:\").Series,
            SystemInterests.For(new DiskHistoryConfig { Scope = DiskScope.AllDrives }, @"C:\").Series);
    }

    [Fact]
    public void S11018_its_drive_and_span_survive_a_save_and_a_clone_and_a_bad_drive_is_repaired()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new DiskHistoryConfig { Drive = "E:\\", GraphSpanSeconds = 900 }, StateJson.WidgetConfig);
        var back = (DiskHistoryConfig)((DiskHistoryConfig)JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!).Clone();
        Assert.Equal(("E:\\", 900), (back.Drive, back.GraphSpanSeconds));

        var bad = new DiskHistoryConfig { Drive = "nope" };
        Assert.Single(bad.Repair());
        Assert.Equal(string.Empty, bad.Drive);
    }

    [Fact]
    public void S11018_its_lines_are_the_drive_then_the_span()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        DiskHistoryModule.Lines(menu, new DiskHistoryConfig(), []);
        Assert.Equal(2, menu.Lines.Count);
    }
}
