using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.CpuDetails.Tests;

/// <summary>[S-9500] The CPU details widget as a module: the feeds it reads and its page of nothing.</summary>
public sealed class CpuDetailsTests
{
    [Fact]
    public void S9500_it_reads_the_load_the_counts_and_the_processor_and_charts_nothing()
    {
        var wants = SystemInterests.For(new CpuDetailsConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Cpu), FeedKey.Create(SystemSource.Performance), FeedKey.Create(SystemSource.Processor)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S9500_it_has_no_settings_page()
    {
        var rows = new List<FrameworkElement>();
        ((IWidgetModule)new CpuDetailsModule()).SettingsRows(new WidgetSettingsRows(rows, () => { }, () => { }), new CpuDetailsConfig());
        Assert.Empty(rows);
    }
}
