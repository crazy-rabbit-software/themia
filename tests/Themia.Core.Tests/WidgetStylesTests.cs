using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Icons;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Widget styles ([S-6000]..[S-6010]): the registry, the stored value and the strip's Style choice.</summary>
public sealed class WidgetStylesTests
{
    private static WidgetInstance Widget(WidgetKind kind, string id)
    {
        var widget = WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1));
        widget.Id = id;
        return widget;
    }

    private static (AppState State, ViewState View) State(params WidgetInstance[] widgets)
    {
        var view = ViewState.Create("v");
        view.Widgets.AddRange(widgets);
        return (new AppState { Views = [view] }, view);
    }

    private static WidgetSelection Selecting(ViewState view, params string[] ids)
    {
        var selection = new WidgetSelection();
        selection.Replace(view.Id, ids);
        return selection;
    }

    [Fact]
    public void S6001_every_styled_kind_is_found_through_its_config_with_sound_options()
    {
        var kinds = WidgetStyles.StyledKinds.ToList();
        Assert.Contains(WidgetKind.Clock, kinds);

        foreach (var kind in Enum.GetValues<WidgetKind>())
        {
            var config = WidgetConfig.CreateDefault(kind);
            var styles = WidgetStyles.For(kind);

            if (config is not IStyled styled)
            {
                Assert.Empty(styles);
                Assert.Null(WidgetStyles.Current(config));
                continue;
            }

            Assert.Same(styled.Styles, styles);
            Assert.True(styles.Count >= 2, $"{kind}: a style choice needs two styles");

            // The default config has the first style; values and names are unique; every glyph is a Fluent icon.
            Assert.Equal(styles[0].Value, styled.StyleValue);
            Assert.Equal(styles.Count, styles.Select(s => s.Value).Distinct().Count());
            Assert.Equal(styles.Count, styles.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(styles, s => Assert.InRange(s.Glyph, 0xE700, 0xF8FF));
            Assert.All(styles, s => Assert.Contains(FluentIcons.All, icon => icon.Code == s.Glyph));
            Assert.All(styles, s => Assert.True(char.IsAsciiLetterLower(s.Name[0]), $"{kind}: '{s.Name}' is not camelCase"));
        }
    }

    [Fact]
    public void S6001_every_styled_config_keeps_its_style_through_a_clone_and_a_save()
    {
        foreach (var kind in WidgetStyles.StyledKinds)
        {
            var styles = WidgetStyles.For(kind);
            var last = styles[^1];
            var widget = Widget(kind, "w");
            Assert.True(WidgetStyles.Set(widget.Config, last.Value));

            Assert.Equal(last.Value, WidgetStyles.Current(widget.Config.Clone()));

            var (state, _) = State(widget);
            var json = JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);
            Assert.Contains($"\"style\": \"{last.Name}\"", json, StringComparison.Ordinal);

            var back = AppStateStore.Parse(json).State!.Views[0].Find("w")!;
            Assert.Equal(last.Value, WidgetStyles.Current(back.Config));
        }
    }

    [Fact]
    public void S6003_setting_a_style_the_kind_does_not_have_writes_nothing()
    {
        var clock = new ClockConfig();

        Assert.False(WidgetStyles.Set(clock, 99));
        Assert.False(WidgetStyles.Set(clock, (int)ClockStyle.Digital));
        Assert.Equal(ClockStyle.Digital, clock.Style);
        Assert.True(WidgetStyles.Set(clock, (int)ClockStyle.Word));
        Assert.Equal(ClockStyle.Word, clock.Style);
        Assert.False(WidgetStyles.Set(new CpuConfig(), 1));
    }

    [Theory]
    [InlineData("\"sundial\"")]
    [InlineData("4")]
    [InlineData("\"7\"")]
    [InlineData("null")]
    [InlineData("{}")]
    public void S6004_an_unknown_stored_style_is_repaired_to_the_first_and_reported(string stored)
    {
        var json = $$"""
            {
              "schemaVersion": 1,
              "views": [ { "id": "v1", "name": "Desktop", "widgets": [
                { "id": "w", "kind": "Clock", "x": 0.1, "y": 0.1, "w": 0.2, "h": 0.2, "config": { "kind": "clock", "style": {{stored}} } }
              ] } ],
              "activeViewId": "v1",
              "monitorViews": {}
            }
            """;

        var parsed = AppStateStore.Parse(json);

        Assert.Equal(ClockStyle.Digital, ((ClockConfig)parsed.State!.Views[0].Find("w")!.Config).Style);
        Assert.Contains(parsed.Repairs, r => r.Contains("style", StringComparison.Ordinal));
    }

    [Fact]
    public void S6004_a_stored_name_reads_in_any_case_and_a_sound_style_needs_no_repair()
    {
        var config = (ClockConfig)ConfigJson.Read("Clock", """{"kind":"clock","style":"Flip"}""");
        Assert.Equal(ClockStyle.Flip, config.Style);
        Assert.Null(WidgetStyles.Repair(config));

        config.Style = (ClockStyle)42;
        Assert.NotNull(WidgetStyles.Repair(config));
        Assert.Equal(ClockStyle.Digital, config.Style);
    }

    [Fact]
    public void S6006_a_style_is_found_by_its_wire_name()
    {
        Assert.Equal((int)ClockStyle.Word, WidgetStyles.Find(WidgetKind.Clock, "WORD")!.Value.Value);
        Assert.Null(WidgetStyles.Find(WidgetKind.Clock, "sundial"));
        Assert.Null(WidgetStyles.Find(WidgetKind.Cpu, "digital"));
    }

    [Fact]
    public void S6007_one_widget_of_a_styled_kind_offers_its_style()
    {
        var clock = Widget(WidgetKind.Clock, "a");
        ((ClockConfig)clock.Config).Style = ClockStyle.Flip;
        var (state, view) = State(clock);

        var offer = QuickSet.For(state, Selecting(view, "a"), view.Id);

        Assert.True(offer.HasStyle);
        Assert.Equal(WidgetKind.Clock, offer.StyleKind);
        Assert.Equal((int)ClockStyle.Flip, offer.Style);
    }

    [Fact]
    public void S6007_a_kind_without_styles_or_a_mixed_kind_selection_offers_none()
    {
        var (state, view) = State(Widget(WidgetKind.Cpu, "cpu"), Widget(WidgetKind.Clock, "clock"));

        Assert.False(QuickSet.For(state, Selecting(view, "cpu"), view.Id).HasStyle);
        Assert.False(QuickSet.For(state, Selecting(view, "cpu", "clock"), view.Id).HasStyle);
    }

    [Fact]
    public void S6007_several_of_one_kind_offer_the_shared_style_or_none_ticked()
    {
        var a = Widget(WidgetKind.Clock, "a");
        var b = Widget(WidgetKind.Clock, "b");
        var (state, view) = State(a, b);

        var same = QuickSet.For(state, Selecting(view, "a", "b"), view.Id);
        Assert.True(same.HasStyle);
        Assert.Equal((int)ClockStyle.Digital, same.Style);

        ((ClockConfig)b.Config).Style = ClockStyle.Analog;
        var mixed = QuickSet.For(state, Selecting(view, "a", "b"), view.Id);
        Assert.True(mixed.HasStyle);
        Assert.Null(mixed.Style);
    }

    [Fact]
    public void S6008_setting_the_style_reaches_every_selected_widget_and_counts_the_changes()
    {
        var a = Widget(WidgetKind.Clock, "a");
        var b = Widget(WidgetKind.Clock, "b");
        var c = Widget(WidgetKind.Clock, "c");
        ((ClockConfig)b.Config).Style = ClockStyle.Minimal;
        var (_, view) = State(a, b, c);

        Assert.Equal(1, QuickSet.SetStyle(view, ["a", "b"], (int)ClockStyle.Minimal));
        Assert.Equal(ClockStyle.Minimal, ((ClockConfig)a.Config).Style);
        Assert.Equal(ClockStyle.Digital, ((ClockConfig)c.Config).Style);
        Assert.Equal(0, QuickSet.SetStyle(view, ["a", "b"], 99));
    }
}
