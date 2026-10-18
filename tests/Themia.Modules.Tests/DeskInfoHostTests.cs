using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-4800] Next meeting and Recent files side by side: their configs and setup through the host's catalogue (Recent files is a module). Moved from Core's DeskInfoWidgetsTests, unchanged.</summary>
public sealed class DeskInfoHostTests
{
    [Theory]
    [InlineData("nextMeeting", typeof(NextMeetingConfig))]
    [InlineData("recentFiles", typeof(RecentFilesConfig))]
    public void S4800_each_kind_has_its_config_and_round_trips(string kindId, Type type)
    {
        var kind = new WidgetId(kindId);
        var config = WidgetConfig.CreateDefault(kind);
        Assert.IsType(type, config);
        Assert.Equal(kind, config.Kind);

        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        var back = JsonSerializer.Deserialize(json, StateJson.WidgetConfig);
        Assert.IsType(type, back);
        Assert.Empty(back!.Repair());
    }

    [Fact]
    public void S4800_only_next_meeting_needs_setting_up_and_only_without_a_calendar()
    {
        Assert.False(WidgetSetup.Needed(new RecentFilesConfig(), FeatureSet.None));
        Assert.True(WidgetSetup.Needed(new NextMeetingConfig(), FeatureSet.None));
        Assert.False(WidgetSetup.Needed(new NextMeetingConfig(), HostFeatures.Calendar));
    }
}
