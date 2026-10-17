using System.Text.Json;
using System.Text.RegularExpressions;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6580]..[S-6585] A widget's content alignment: which kinds and styles take it, what each does
/// without a choice, what the card draws, the properties panel's offer and writes, and load repair.
/// The list (<see cref="ContentAlignment"/>) is checked against the views that take it.
/// </summary>
public sealed partial class ContentAlignmentTests
{
    /// <summary>A view class that declares it aligns: the interface in its base list.</summary>
    [GeneratedRegex(@"partial class (\w+)\s*:[^{]*\bIWidgetAligned\b")]
    private static partial Regex AlignedView();

    private static WidgetInstance Widget(WidgetId kind, int? style = null)
    {
        var widget = WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1));
        if (style is { } value)
        {
            Assert.True(WidgetStyles.Set(widget.Config, value) || WidgetStyles.Current(widget.Config) == value);
        }

        return widget;
    }

    [Fact]
    public void S6581_the_list_matches_the_views_that_take_an_alignment_both_ways()
    {
        var views = ModuleSources.Views();

        var aligned = RepoFiles.ViewSources()
            .SelectMany(f => AlignedView().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        var wrong = new List<string>();
        foreach (var kind in ModuleCatalog.Widgets.Select(w => w.Id).ToArray())
        {
            var takes = views.TryGetValue(kind, out var view) && aligned.Contains(view);
            if (takes != ContentAlignment.Offers(kind))
            {
                wrong.Add($"{kind} ({view ?? "no named arm"}) takes an alignment: {takes}, listed: {ContentAlignment.Offers(kind)}");
            }
        }

        Assert.True(wrong.Count == 0, "ContentAlignment disagrees with the views:\n" + string.Join('\n', wrong));
    }

    [Fact]
    public void S6581_readouts_align_and_lists_charts_and_buttons_do_not()
    {
        WidgetId[] aligned = [ClockConfig.Descriptor.Id, CountdownConfig.Descriptor.Id, WeatherNowConfig.Descriptor.Id, StocksConfig.Descriptor.Id, new WidgetId("quote"), new WidgetId("moon"), AirQualityConfig.Descriptor.Id, ExchangeRatesConfig.Descriptor.Id];
        Assert.Equal(aligned.Order(), ContentAlignment.All);

        foreach (var kind in (WidgetId[])[TasksConfig.Descriptor.Id, EmailConfig.Descriptor.Id, CalendarWidgetConfig.Descriptor.Id, CpuConfig.Descriptor.Id, MixerConfig.Descriptor.Id, WifiToggleConfig.Descriptor.Id, ShutdownConfig.Descriptor.Id, FolderConfig.Descriptor.Id])
        {
            Assert.False(ContentAlignment.Offers(kind), $"{kind}");
            Assert.False(ContentAlignment.Applies(Widget(kind)), $"{kind}");
        }
    }

    [Fact]
    public void S6581_some_styles_of_a_kind_align_and_others_do_not()
    {
        Assert.True(ContentAlignment.Applies(Widget(ExchangeRatesConfig.Descriptor.Id, (int)ExchangeRatesStyle.Number)));
        Assert.False(ContentAlignment.Applies(Widget(ExchangeRatesConfig.Descriptor.Id, (int)ExchangeRatesStyle.Chart)));
        Assert.True(ContentAlignment.Applies(Widget(StocksConfig.Descriptor.Id, (int)StocksStyle.Number)));
        Assert.False(ContentAlignment.Applies(Widget(StocksConfig.Descriptor.Id, (int)StocksStyle.Chart)));
        Assert.True(ContentAlignment.Applies(Widget(new WidgetId("moon"), (int)MoonStyle.Disc)));
        Assert.False(ContentAlignment.Applies(Widget(new WidgetId("moon"), (int)MoonStyle.PhaseMonth)));

        // Every face of the clock, the weather and the air quality has one block to place.
        foreach (var style in Enum.GetValues<ClockStyle>())
        {
            Assert.True(ContentAlignment.Applies(Widget(ClockConfig.Descriptor.Id, (int)style)), $"{style}");
        }

        foreach (var style in Enum.GetValues<WeatherNowStyle>())
        {
            Assert.True(ContentAlignment.Applies(Widget(WeatherNowConfig.Descriptor.Id, (int)style)), $"{style}");
        }

        Assert.True(ContentAlignment.Applies(Widget(AirQualityConfig.Descriptor.Id, (int)AirQualityStyle.Gauge)));
    }

    [Fact]
    public void S6582_without_a_choice_each_widget_sits_where_it_always_did()
    {
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(Widget(ClockConfig.Descriptor.Id)));
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(Widget(new WidgetId("moon"))));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(Widget(WeatherNowConfig.Descriptor.Id, (int)WeatherNowStyle.Standard)));
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(Widget(WeatherNowConfig.Descriptor.Id, (int)WeatherNowStyle.LargeIcon)));
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(Widget(WeatherNowConfig.Descriptor.Id, (int)WeatherNowStyle.Minimal)));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(Widget(AirQualityConfig.Descriptor.Id, (int)AirQualityStyle.Number)));
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(Widget(AirQualityConfig.Descriptor.Id, (int)AirQualityStyle.Gauge)));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(Widget(new WidgetId("quote"))));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(Widget(CountdownConfig.Descriptor.Id)));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(Widget(ExchangeRatesConfig.Descriptor.Id, (int)ExchangeRatesStyle.Number)));

        // A new widget stores no choice: its default follows its style.
        Assert.Null(Widget(WeatherNowConfig.Descriptor.Id).Align);
    }

    [Fact]
    public void S6583_the_card_draws_the_choice_only_where_the_style_takes_one()
    {
        var weather = Widget(WeatherNowConfig.Descriptor.Id);
        weather.Align = ContentAlign.Right;
        Assert.Equal(ContentAlign.Right, ContentAlignment.Shown(weather));

        // The moon's month keeps the choice, and draws its own default, until the disc is back.
        var moon = Widget(new WidgetId("moon"), (int)MoonStyle.PhaseMonth);
        moon.Align = ContentAlign.Left;
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(moon));
        Assert.True(WidgetStyles.Set(moon.Config, (int)MoonStyle.Disc));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(moon));

        // A value outside the set never reaches a view.
        var clock = Widget(ClockConfig.Descriptor.Id);
        clock.Align = (ContentAlign)9;
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(clock));
    }

    private static (AppState State, ViewState View, WidgetSelection Selection) Selected(params WidgetInstance[] widgets)
    {
        var view = ViewState.Create("v");
        for (var i = 0; i < widgets.Length; i++)
        {
            widgets[i].Id = $"w{i}";
            view.Widgets.Add(widgets[i]);
        }

        var selection = new WidgetSelection();
        selection.Replace(view.Id, [.. view.Widgets.Select(w => w.Id)]);
        return (new AppState { Views = [view] }, view, selection);
    }

    [Fact]
    public void S6584_one_widget_offers_where_it_sits_now_and_a_pick_stores_it()
    {
        var (state, view, selection) = Selected(Widget(ClockConfig.Descriptor.Id));

        var offer = QuickSet.For(state, selection, view.Id);
        Assert.True(offer.CanAlign);
        Assert.Equal(ContentAlign.Center, offer.Align);

        Assert.Equal(1, QuickSet.SetAlign(view, ["w0"], ContentAlign.Left));
        Assert.Equal(ContentAlign.Left, view.Widgets[0].Align);
        Assert.Equal(ContentAlign.Left, QuickSet.For(state, selection, view.Id).Align);

        // The same pick again changes nothing: no undo step.
        Assert.Equal(0, QuickSet.SetAlign(view, ["w0"], ContentAlign.Left));
    }

    [Fact]
    public void S6584_a_mixed_selection_reads_a_dash_and_a_pick_reaches_only_the_widgets_that_align()
    {
        var quote = Widget(new WidgetId("quote"));
        quote.Align = ContentAlign.Right;
        var (state, view, selection) = Selected(Widget(ClockConfig.Descriptor.Id), quote, Widget(TasksConfig.Descriptor.Id), Widget(new WidgetId("moon"), (int)MoonStyle.PhaseMonth));

        var offer = QuickSet.For(state, selection, view.Id);
        Assert.True(offer.CanAlign);
        Assert.Null(offer.Align);

        Assert.Equal(1, QuickSet.SetAlign(view, ["w0", "w1", "w2", "w3"], ContentAlign.Right));
        Assert.Equal(ContentAlign.Right, view.Widgets[0].Align);
        Assert.Null(view.Widgets[2].Align);
        Assert.Null(view.Widgets[3].Align);
        Assert.Equal(ContentAlign.Right, QuickSet.For(state, selection, view.Id).Align);
    }

    [Fact]
    public void S6584_the_row_is_greyed_for_a_widget_with_nothing_to_align_and_says_why()
    {
        var (state, view, selection) = Selected(Widget(TasksConfig.Descriptor.Id));
        var tasks = QuickSet.For(state, selection, view.Id);
        Assert.False(tasks.CanAlign);
        Assert.False(tasks.AlignKind);

        // The moon aligns, only not in its month style: the row says so.
        (state, view, selection) = Selected(Widget(new WidgetId("moon"), (int)MoonStyle.PhaseMonth));
        var month = QuickSet.For(state, selection, view.Id);
        Assert.False(month.CanAlign);
        Assert.True(month.AlignKind);
    }

    [Fact]
    public void S6585_load_drops_an_alignment_outside_the_set_or_on_a_kind_without_one()
    {
        var clock = Widget(ClockConfig.Descriptor.Id);
        clock.Align = (ContentAlign)9;
        Assert.NotNull(ContentAlignment.Repair(clock));
        Assert.Null(clock.Align);

        var tasks = Widget(TasksConfig.Descriptor.Id);
        tasks.Align = ContentAlign.Right;
        Assert.NotNull(ContentAlignment.Repair(tasks));
        Assert.Null(tasks.Align);

        // A choice on a style that does not take it is kept for the style that does.
        var moon = Widget(new WidgetId("moon"), (int)MoonStyle.PhaseMonth);
        moon.Align = ContentAlign.Left;
        Assert.Null(ContentAlignment.Repair(moon));
        Assert.Equal(ContentAlign.Left, moon.Align);

        var none = Widget(ClockConfig.Descriptor.Id);
        Assert.Null(ContentAlignment.Repair(none));
    }

    [Fact]
    public void S6580_the_choice_is_saved_by_name_survives_a_copy_and_none_is_left_out()
    {
        var clock = Widget(ClockConfig.Descriptor.Id);
        var bare = JsonSerializer.Serialize(clock, StateJson.WidgetInstance);
        Assert.DoesNotContain("\"align\"", bare, StringComparison.Ordinal);

        clock.Align = ContentAlign.Right;
        var json = JsonSerializer.Serialize(clock, StateJson.WidgetInstance);
        Assert.Contains("\"align\": \"Right\"", json, StringComparison.Ordinal);

        var back = JsonSerializer.Deserialize(json, StateJson.WidgetInstance);
        Assert.Equal(ContentAlign.Right, back?.Align);
        Assert.Equal(ContentAlign.Right, clock.Clone().Align);
    }
}
