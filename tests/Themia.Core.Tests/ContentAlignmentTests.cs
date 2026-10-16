using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6580]..[S-6585] A widget's content alignment: which kinds and styles take it, what each does
/// without a choice, what the card draws, the properties panel's offer and writes, and load repair.
/// The list (<see cref="ContentAlignment"/>) is checked against the views that take it.
/// </summary>
public sealed partial class ContentAlignmentTests
{
    private static readonly string App = Path.Combine(FindRoot(), "src", "Themia.App");

    /// <summary>Walks up from this source file, not the test binaries, which may build outside the repository.</summary>
    private static string FindRoot([CallerFilePath] string here = "")
    {
        for (var dir = new FileInfo(here).Directory; dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "Themia.App", "Widgets")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("repository root (src/Themia.App/Widgets) not found above this test's source");
    }

    /// <summary>One arm of <c>WidgetCatalog.CreateView</c>: the kinds it names, and the view it builds.</summary>
    [GeneratedRegex(@"((?:WidgetKind\.\w+\s*(?:or\s*)?)+)=> new (\w+)\(")]
    private static partial Regex ViewArm();

    /// <summary>A view class that declares it aligns: the interface in its base list.</summary>
    [GeneratedRegex(@"partial class (\w+)\s*:[^{]*\bIWidgetAligned\b")]
    private static partial Regex AlignedView();

    private static WidgetInstance Widget(WidgetKind kind, int? style = null)
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
        var catalog = File.ReadAllText(Path.Combine(App, "Widgets", "WidgetCatalog.cs"));
        var factory = catalog[catalog.IndexOf("CreateView(WidgetKind kind)", StringComparison.Ordinal)..];
        var views = new Dictionary<WidgetKind, string>();
        foreach (Match arm in ViewArm().Matches(factory))
        {
            foreach (Match named in Regex.Matches(arm.Groups[1].Value, @"WidgetKind\.(\w+)"))
            {
                views.TryAdd(Enum.Parse<WidgetKind>(named.Groups[1].Value), arm.Groups[2].Value);
            }
        }

        var aligned = Directory.EnumerateFiles(Path.Combine(App, "Widgets"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => AlignedView().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        var wrong = new List<string>();
        foreach (var kind in Enum.GetValues<WidgetKind>())
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
        Assert.Equal(
            [WidgetKind.Clock, WidgetKind.Countdown, WidgetKind.WeatherNow, WidgetKind.Stocks, WidgetKind.Quote, WidgetKind.Moon, WidgetKind.AirQuality, WidgetKind.ExchangeRates],
            ContentAlignment.All);

        foreach (var kind in (WidgetKind[])[WidgetKind.Tasks, WidgetKind.Email, WidgetKind.Calendar, WidgetKind.Cpu, WidgetKind.Mixer, WidgetKind.WifiToggle, WidgetKind.Shutdown, WidgetKind.Folder])
        {
            Assert.False(ContentAlignment.Offers(kind), $"{kind}");
            Assert.False(ContentAlignment.Applies(Widget(kind)), $"{kind}");
        }
    }

    [Fact]
    public void S6581_some_styles_of_a_kind_align_and_others_do_not()
    {
        Assert.True(ContentAlignment.Applies(Widget(WidgetKind.ExchangeRates, (int)ExchangeRatesStyle.Number)));
        Assert.False(ContentAlignment.Applies(Widget(WidgetKind.ExchangeRates, (int)ExchangeRatesStyle.Chart)));
        Assert.True(ContentAlignment.Applies(Widget(WidgetKind.Stocks, (int)StocksStyle.Number)));
        Assert.False(ContentAlignment.Applies(Widget(WidgetKind.Stocks, (int)StocksStyle.Chart)));
        Assert.True(ContentAlignment.Applies(Widget(WidgetKind.Moon, (int)MoonStyle.Disc)));
        Assert.False(ContentAlignment.Applies(Widget(WidgetKind.Moon, (int)MoonStyle.PhaseMonth)));

        // Every face of the clock, the weather and the air quality has one block to place.
        foreach (var style in Enum.GetValues<ClockStyle>())
        {
            Assert.True(ContentAlignment.Applies(Widget(WidgetKind.Clock, (int)style)), $"{style}");
        }

        foreach (var style in Enum.GetValues<WeatherNowStyle>())
        {
            Assert.True(ContentAlignment.Applies(Widget(WidgetKind.WeatherNow, (int)style)), $"{style}");
        }

        Assert.True(ContentAlignment.Applies(Widget(WidgetKind.AirQuality, (int)AirQualityStyle.Gauge)));
    }

    [Fact]
    public void S6582_without_a_choice_each_widget_sits_where_it_always_did()
    {
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(Widget(WidgetKind.Clock)));
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(Widget(WidgetKind.Moon)));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(Widget(WidgetKind.WeatherNow, (int)WeatherNowStyle.Standard)));
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(Widget(WidgetKind.WeatherNow, (int)WeatherNowStyle.LargeIcon)));
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(Widget(WidgetKind.WeatherNow, (int)WeatherNowStyle.Minimal)));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(Widget(WidgetKind.AirQuality, (int)AirQualityStyle.Number)));
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(Widget(WidgetKind.AirQuality, (int)AirQualityStyle.Gauge)));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(Widget(WidgetKind.Quote)));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(Widget(WidgetKind.Countdown)));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(Widget(WidgetKind.ExchangeRates, (int)ExchangeRatesStyle.Number)));

        // A new widget stores no choice: its default follows its style.
        Assert.Null(Widget(WidgetKind.WeatherNow).Align);
    }

    [Fact]
    public void S6583_the_card_draws_the_choice_only_where_the_style_takes_one()
    {
        var weather = Widget(WidgetKind.WeatherNow);
        weather.Align = ContentAlign.Right;
        Assert.Equal(ContentAlign.Right, ContentAlignment.Shown(weather));

        // The moon's month keeps the choice, and draws its own default, until the disc is back.
        var moon = Widget(WidgetKind.Moon, (int)MoonStyle.PhaseMonth);
        moon.Align = ContentAlign.Left;
        Assert.Equal(ContentAlign.Center, ContentAlignment.Shown(moon));
        Assert.True(WidgetStyles.Set(moon.Config, (int)MoonStyle.Disc));
        Assert.Equal(ContentAlign.Left, ContentAlignment.Shown(moon));

        // A value outside the set never reaches a view.
        var clock = Widget(WidgetKind.Clock);
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
        var (state, view, selection) = Selected(Widget(WidgetKind.Clock));

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
        var quote = Widget(WidgetKind.Quote);
        quote.Align = ContentAlign.Right;
        var (state, view, selection) = Selected(Widget(WidgetKind.Clock), quote, Widget(WidgetKind.Tasks), Widget(WidgetKind.Moon, (int)MoonStyle.PhaseMonth));

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
        var (state, view, selection) = Selected(Widget(WidgetKind.Tasks));
        var tasks = QuickSet.For(state, selection, view.Id);
        Assert.False(tasks.CanAlign);
        Assert.False(tasks.AlignKind);

        // The moon aligns, only not in its month style: the row says so.
        (state, view, selection) = Selected(Widget(WidgetKind.Moon, (int)MoonStyle.PhaseMonth));
        var month = QuickSet.For(state, selection, view.Id);
        Assert.False(month.CanAlign);
        Assert.True(month.AlignKind);
    }

    [Fact]
    public void S6585_load_drops_an_alignment_outside_the_set_or_on_a_kind_without_one()
    {
        var clock = Widget(WidgetKind.Clock);
        clock.Align = (ContentAlign)9;
        Assert.NotNull(ContentAlignment.Repair(clock));
        Assert.Null(clock.Align);

        var tasks = Widget(WidgetKind.Tasks);
        tasks.Align = ContentAlign.Right;
        Assert.NotNull(ContentAlignment.Repair(tasks));
        Assert.Null(tasks.Align);

        // A choice on a style that does not take it is kept for the style that does.
        var moon = Widget(WidgetKind.Moon, (int)MoonStyle.PhaseMonth);
        moon.Align = ContentAlign.Left;
        Assert.Null(ContentAlignment.Repair(moon));
        Assert.Equal(ContentAlign.Left, moon.Align);

        var none = Widget(WidgetKind.Clock);
        Assert.Null(ContentAlignment.Repair(none));
    }

    [Fact]
    public void S6580_the_choice_is_saved_by_name_survives_a_copy_and_none_is_left_out()
    {
        var clock = Widget(WidgetKind.Clock);
        var bare = JsonSerializer.Serialize(clock, AppStateJsonContext.Default.WidgetInstance);
        Assert.DoesNotContain("\"align\"", bare, StringComparison.Ordinal);

        clock.Align = ContentAlign.Right;
        var json = JsonSerializer.Serialize(clock, AppStateJsonContext.Default.WidgetInstance);
        Assert.Contains("\"align\": \"Right\"", json, StringComparison.Ordinal);

        var back = JsonSerializer.Deserialize(json, AppStateJsonContext.Default.WidgetInstance);
        Assert.Equal(ContentAlign.Right, back?.Align);
        Assert.Equal(ContentAlign.Right, clock.Clone().Align);
    }
}
