using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.MusicProgress;
using Themia.Widget.NextMeeting;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-12833] Every one-reading face states its parts and goes down to its main reading at 9 (owner, 2026-11-19: "for the
/// widgets that can scale there should basically be no lower limit except for one that makes sense so you can still see
/// the widget"; "do this for the other widgets too"): its captions, units, glyphs and controls give way first.
/// </summary>
public sealed class ReadingFaceFloorTests
{
    /// <summary>A kind in a style: its config.</summary>
    private static WidgetConfig Config(string kind, string style)
    {
        var id = new WidgetId(kind);
        var config = ModuleCatalog.Of(id).CreateDefault();
        if (style != "default" && WidgetStyles.Find(id, style) is { } found)
        {
            WidgetStyles.Set(config, found.Value);
        }

        return config;
    }

    /// <summary>The parts a kind states for its face: the same its sizing and its view take.</summary>
    private static FaceParts? Face(string kind, WidgetConfig config) => kind switch
    {
        "airQuality" => AirLayout.Face((AirQualityConfig)config),
        "exchangeRate" => RatesLayout.Face(),
        "screenTime" => ScreenTimeReading.Face(),
        "recycleBin" => RecycleBinLayout.Face(),
        "moon" => MoonLayout.Face((MoonConfig)config),
        "brightness" => LevelFace.Face(under: false),
        "volume" => LevelFace.Face(under: true),
        "alarm" => AlarmFace.Face((AlarmConfig)config, Strings.Current),
        "focusSession" => FocusSessionLayout.Face((FocusSessionConfig)config),
        "countdown" => CountdownLayout.Face((CountdownConfig)config, Strings.Current),
        "rainOutlook" => TextRules.Face(2, lead: false),
        "weatherCondition" => TextRules.Face(1, lead: false),
        "nextMeeting" => NextMeetingJoin.Face((NextMeetingConfig)config),
        _ => null,
    };

    /// <summary>
    /// Each one-reading face in each style: the most its least height may be now and what it was (2026-11-19, the
    /// caption's floor: its natural box at 0.75; the bar drive's ring keeps its own).
    /// </summary>
    public static TheoryData<string, string, double, double> Faces() => new()
    {
        { "airQuality", "number", 32, 57 },
        { "airQuality", "gauge", 60, 91 },
        { "exchangeRate", "default", 32, 57 },
        { "screenTime", "default", 32, 57 },
        { "recycleBin", "default", 32, 84 },
        { "moon", "disc", 36, 57 },
        { "moon", "phaseMonth", 113, 141 },   // [S-15805] its cells' discs at the glyph floor under its name
        { "brightness", "default", 32, 72 },
        { "volume", "default", 32, 84 },
        { "alarm", "default", 36, 131 },
        { "focusSession", "default", 36, 131 },
        { "countdown", "text", 40, 104 },
        { "countdown", "ring", 104, 281 },
        { "countdown", "flip", 40, 74 },
        { "rainOutlook", "default", 48, 80 },
        { "weatherCondition", "default", 48, 52 },
        { "nextMeeting", "default", 32, 93 },
        { "cpu", "number", 45, 45 },
        { "gpu", "number", 45, 45 },
        { "memory", "number", 45, 45 },
        { "disk", "number", 45, 45 },
        { "ping", "number", 45, 45 },
        { "battery", "number", 45, 45 },
    };

    [Theory]
    [MemberData(nameof(Faces))]
    public void S12833_each_one_reading_faces_least_card_is_its_main_reading_at_9(string kind, string style, double most, double before)
    {
        var sizing = ModuleCatalog.Of(new WidgetId(kind)).Sizing(Config(kind, style));

        Assert.InRange(sizing.Min.Height, WidgetSizing.LeastScaled, most);
        Assert.True(sizing.Min.Height <= before, $"{kind} {style}: {sizing.Min.Height} over {before}");
        Assert.True(sizing.Min.Width >= WidgetSizing.LeastScaled && sizing.Min.Width <= sizing.Natural.Width, $"{kind} {style}");
        Assert.Empty(sizing.Problems());
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public void S12833_each_face_draws_every_part_at_its_natural_box_and_its_parts_only_go_as_its_card_shrinks(string kind, string style, double most, double before)
    {
        _ = (most, before);
        var config = Config(kind, style);
        if (Face(kind, config) is not { } face)
        {
            return;
        }

        var sizing = ModuleCatalog.Of(new WidgetId(kind)).Sizing(config);
        var over = kind == "screenTime" ? ScreenTimeReading.DayLine(((ScreenTimeConfig)config).Period) : 0;
        var natural = face.Fit(sizing.Natural.Width - 24, sizing.Natural.Height - 24 - over);
        Assert.All(face.Sizes, size => Assert.True(natural.Draws(size), $"{kind} {style}: {size} gave way at its natural box"));

        // Its fit fills the card (WR-1, WR-4: the block grows back into the room a part left), so its scale may rise where a
        // part goes; a part that went never comes back, and nothing it draws is under 9.
        var lastFloor = 0.0;
        var least = face.Fit(sizing.Min.Width - 24, sizing.Min.Height - 24 - over);
        for (var t = 1.0; t >= 0; t -= 0.02)
        {
            var width = sizing.Min.Width + (t * (sizing.Natural.Width - sizing.Min.Width)) - 24;
            var height = sizing.Min.Height + (t * (sizing.Natural.Height - sizing.Min.Height)) - 24 - over;
            var fit = face.Fit(width, height);
            Assert.True(fit.Floor >= lastFloor, $"{kind} {style} {width}x{height}: a part came back");
            Assert.All(face.Sizes.Where(fit.Draws), size => Assert.True(size * fit.Scale >= ScaleFloor.Text - 1e-6, $"{kind} {style}: {size} at {size * fit.Scale}"));
            lastFloor = fit.Floor;
        }

        // At its least card the main reading alone, at the floor.
        Assert.All(face.Sizes.Where(s => s < face.Main), size => Assert.False(least.Draws(size), $"{kind} {style}: {size} at its least"));
        Assert.InRange(least.Scale * face.Main, ScaleFloor.Text - 1e-6, ScaleFloor.Text + 3);
    }
}
