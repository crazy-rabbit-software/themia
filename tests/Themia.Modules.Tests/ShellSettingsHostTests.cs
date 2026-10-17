using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-700] Header defaults per kind, over widgets that are module projects (only this host has
/// them). The rest of the shell settings' tests are Core's ShellSettingsTests.
/// </summary>
public sealed class ShellSettingsHostTests
{
    /// <summary>A small valid state file holding one clock.</summary>
    private const string StateFile = """
        {
          "schemaVersion": 1,
          "views": [
            {
              "id": "v1",
              "name": "Desktop",
              "widgets": [
                { "id": "w1", "kind": "Clock", "x": 0.1, "y": 0.1, "w": 0.2, "h": 0.2, "config": { "kind": "clock" } }
              ]
            }
          ],
          "activeViewId": "v1",
          "monitorViews": {},
          "design": { "widgetGapPx": 8, "introAnimation": "Fade", "introAnimationMs": 400 }
        }
        """;

    private static AppState Parse(string json)
    {
        var result = AppStateStore.Parse(json);
        Assert.Equal(ParseRefusal.None, result.Refusal);
        return result.State!;
    }

    [Fact]
    public void S700_the_clock_starts_without_a_header()
        => Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.Create(new WidgetId("clock"), new FracRect(0, 0, 0.1, 0.1)).Header);

    [Theory]
    [InlineData("sun")]
    [InlineData("moon")]
    [InlineData("drive")]
    public void S700_the_sun_the_moon_and_the_drive_card_start_without_a_header(string kindId)
    {
        var kind = new WidgetId(kindId);
        Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1)).Header);
    }

    [Theory]
    [InlineData("cpu")]
    [InlineData("disk")]
    [InlineData("notes")]
    [InlineData("countdown")]
    [InlineData("pomodoro")]
    public void S700_other_widgets_still_start_with_a_top_header(string kindId)
    {
        var kind = new WidgetId(kindId);
        Assert.Equal(HeaderPlacement.Top, WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1)).Header);
    }

    [Fact]
    public void S700_a_saved_clock_keeps_the_header_it_was_saved_with()
    {
        var state = Parse(StateFile.Replace("\"kind\": \"Clock\",", "\"kind\": \"Clock\", \"header\": \"Top\",", StringComparison.Ordinal));

        Assert.Equal(HeaderPlacement.Top, state.Views[0].Widgets[0].Header);
    }
}
