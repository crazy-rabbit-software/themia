using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Series;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.WeatherFacts;

[assembly: ThemiaWidget("weatherDay", typeof(WeatherDayModule))]
[assembly: ThemiaWidget("weatherHigh", typeof(WeatherHighModule))]
[assembly: ThemiaWidget("weatherLow", typeof(WeatherLowModule))]
[assembly: ThemiaWidget("weatherRange", typeof(WeatherRangeModule))]

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13919] A day's part: what a weather forecast's day is put together of (owner, 2026-11-21: "it should actually be based on
/// the text and icon widgets of the existing atoms"; "add a temperature range bar component to the forecast as well"): its
/// name, its sky, its high, its low and where they stand on the forecast's range. Each a widget of its own, at its place and
/// on its day; in a forecast both are the forecast's ([S-15529]).
/// </summary>
public enum DayPart
{
    /// <summary>"Today", or the weekday ("Tue"): the text face.</summary>
    Day,

    /// <summary>The day's high, "16°": the text face.</summary>
    High,

    /// <summary>The day's low, "8°": the text face, the secondary role.</summary>
    Low,

    /// <summary>The day's low to high as a bar on the scale of every day the forecast holds.</summary>
    Range,
}

/// <summary>[S-13919] The day parts' rules: what each shows of a reading's day. Pure.</summary>
public static class DayParts
{
    /// <summary>[S-13919] A text part's words for day <paramref name="day"/> from today; <see cref="WeatherFactRules.Missing"/> past the forecast or without the value.</summary>
    public static string Text(Locale locale, DayPart part, CurrentWeather reading, int day)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var on = WeatherFactRules.DayOf(reading, day);
        var today = WeatherFactRules.DayOf(reading, 0);
        var text = on is null ? null : part switch
        {
            DayPart.Day => today is null ? null : WeatherReport.DayLabel(locale, on.Day, today.Day),
            DayPart.High => on.High is null ? null : WeatherReport.Degrees(on.High),
            DayPart.Low => on.Low is null ? null : WeatherReport.Degrees(on.Low),
            _ => null,
        };
        return string.IsNullOrEmpty(text) ? WeatherFactRules.Missing : text;
    }

    /// <summary>[S-13919] The sky's glyph on day <paramref name="day"/>: its forecast code's (today's, the code now where the day has none); null without one.</summary>
    public static string? SkyGlyph(CurrentWeather reading, int day)
    {
        ArgumentNullException.ThrowIfNull(reading);
        return WeatherReport.SkyCode(reading, day) is { } code ? WeatherGlyph.Of(WeatherReport.Look(code)) : null;
    }

    /// <summary>
    /// [S-13920] Where day <paramref name="day"/>'s low and high stand between the lowest low and the highest high of every day
    /// the reading holds, as fractions 0..1 (the same scale on every day, so a column of bars compares); null without both.
    /// </summary>
    public static (double From, double To)? Range(CurrentWeather reading, int day)
    {
        ArgumentNullException.ThrowIfNull(reading);
        if (WeatherFactRules.DayOf(reading, day) is not { Low: { } low, High: { } high })
        {
            return null;
        }

        var lows = reading.Days.Where(d => d.Low is not null).Select(d => d.Low!.Value).Append(low);
        var highs = reading.Days.Where(d => d.High is not null).Select(d => d.High!.Value).Append(high);
        var (min, max) = (lows.Min(), highs.Max());
        if (!(max - min > 1e-9))
        {
            return (0, 1);
        }

        var from = Math.Clamp((Math.Min(low, high) - min) / (max - min), 0, 1);
        var to = Math.Clamp((Math.Max(low, high) - min) / (max - min), 0, 1);
        return (from, to);
    }
}

/// <summary>[S-13919] What a day part remembers: its place (the weather family's) and its day; in a forecast both are the forecast's.</summary>
public abstract class WeatherDayPartConfig(DayPart part) : WeatherConfig, IInterests, IDayChoice
{
    /// <summary>The part it is: its kind's.</summary>
    [JsonIgnore]
    public DayPart Part { get; } = part;

    [JsonIgnore]
    public override bool ReadsWeather => true;

    /// <summary>[S-15513] The day from today it shows: 0 today.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Day { get; set; }

    /// <summary>Every part means something for a day ahead.</summary>
    [JsonIgnore]
    public bool TakesDay => true;

    /// <summary>[S-2608] What the background sync keeps fresh for it: its place's reading with its days.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        ArgumentNullException.ThrowIfNull(interests);
        if (WeatherInterests.For(this, interests.Unit, UpdateCadence.Interval(interests.Refresh, HostCadences.Weather)) is { } wants)
        {
            interests.Of<WeatherInterest>().Add(wants);
        }
    }

    /// <summary>[S-15513] A day past the forecast is today, loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());
        if (Day < 0 || Day >= DayRangeSource.Reach)
        {
            notes.Add($"{GetType().Name}: day {Day} is not one it shows; today");
            Day = 0;
        }

        return notes;
    }

    public override WidgetConfig Clone()
    {
        var copy = WeatherDayPartKinds.Create(Part);
        CopyTo(copy);
        copy.Day = Day;
        return copy;
    }
}

/// <summary>[S-6583] A text part: its words sit at the start, the middle or the end of its box (an icon and a bar fill theirs).</summary>
public abstract class WeatherDayTextConfig(DayPart part) : WeatherDayPartConfig(part), IContentAligned;

public sealed class WeatherDayConfig() : WeatherDayTextConfig(DayPart.Day);

public sealed class WeatherHighConfig() : WeatherDayTextConfig(DayPart.High);

public sealed class WeatherLowConfig() : WeatherDayTextConfig(DayPart.Low);

public sealed class WeatherRangeConfig() : WeatherDayPartConfig(DayPart.Range);

/// <summary>[S-13919] The day parts as data: the text ones' kinds (the one text face, [S-14930]), each part's config.</summary>
internal static class WeatherDayPartKinds
{
    /// <summary>Their run among the weather facts on the add page: after the facts.</summary>
    public const int Rank = 100;

    public static TextKind Day { get; } = new("weatherDay", "day", Glyphs.Calendar,
        WeatherFactsStr.WeatherFacts_Day_Label.Local(), WeatherFactsStr.WeatherFacts_Day_Desc.Local(),
        ["day", "weekday", "today", "forecast", "weather"], () => new WeatherDayConfig())
    {
        Lead = false,
        Width = TextRules.ShortWidth,
    };

    public static TextKind High { get; } = new("weatherHigh", "high", Glyphs.Weather,
        WeatherFactsStr.WeatherFacts_High_Label.Local(), WeatherFactsStr.WeatherFacts_High_Desc.Local(),
        ["high", "maximum", "warmest", "temperature", "forecast", "weather"], () => new WeatherHighConfig())
    {
        Lead = false,
        Width = TextRules.ShortWidth,
    };

    public static TextKind Low { get; } = new("weatherLow", "low", Glyphs.Weather,
        WeatherFactsStr.WeatherFacts_Low_Label.Local(), WeatherFactsStr.WeatherFacts_Low_Desc.Local(),
        ["low", "minimum", "coldest", "temperature", "forecast", "weather"], () => new WeatherLowConfig())
    {
        Lead = false,
        Tone = TextTone.Secondary,
        Width = TextRules.ShortWidth,
    };

    /// <summary>A part's config, fresh.</summary>
    public static WeatherDayPartConfig Create(DayPart part) => part switch
    {
        DayPart.Day => new WeatherDayConfig(),
        DayPart.High => new WeatherHighConfig(),
        DayPart.Low => new WeatherLowConfig(),
        _ => new WeatherRangeConfig(),
    };

    /// <summary>[S-13919] A part's page: its place (the weather family's) and its day; in a forecast both go ([S-15529]).</summary>
    public static void Lines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (config is not WeatherDayPartConfig part)
        {
            return;
        }

        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, part));
        var days = Enumerable.Range(0, DayRangeSource.Reach).Select(d => (d, WeatherFactModule.DayName(d))).ToList();
        menu.Lines.Add(WidgetMenuLines.Shared(SharedSettings.Day, menu.Choice(Glyphs.Calendar, WeatherFactsStr.WeatherFacts_Day_Line.Text(), days, part.Day, d => part.Day = d)));
    }
}

/// <summary>[S-13919] The day's name on the text face.</summary>
public sealed class WeatherDayModule() : TextModule(WeatherDayPartKinds.Day, WeatherFactJsonContext.Default, AddFolderId.WeatherFacts, WeatherDayPartKinds.Rank)
{
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => WeatherDayPartKinds.Lines(menu, config);

    public override FrameworkElement CreateView(IWidgetHost host) => new WeatherDayTextView(host, WeatherDayPartKinds.Day, DayPart.Day);
}

/// <summary>[S-13919] The day's high on the text face.</summary>
public sealed class WeatherHighModule() : TextModule(WeatherDayPartKinds.High, WeatherFactJsonContext.Default, AddFolderId.WeatherFacts, WeatherDayPartKinds.Rank + 2)
{
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => WeatherDayPartKinds.Lines(menu, config);

    public override FrameworkElement CreateView(IWidgetHost host) => new WeatherDayTextView(host, WeatherDayPartKinds.High, DayPart.High);
}

/// <summary>[S-13919] The day's low on the text face.</summary>
public sealed class WeatherLowModule() : TextModule(WeatherDayPartKinds.Low, WeatherFactJsonContext.Default, AddFolderId.WeatherFacts, WeatherDayPartKinds.Rank + 3)
{
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => WeatherDayPartKinds.Lines(menu, config);

    public override FrameworkElement CreateView(IWidgetHost host) => new WeatherDayTextView(host, WeatherDayPartKinds.Low, DayPart.Low);
}

/// <summary>[S-13920] The day's low to high as a bar on the forecast's scale.</summary>
public sealed class WeatherRangeModule : IWidgetModule
{
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("weatherRange"),
        ConfigType = typeof(WeatherRangeConfig),
        CreateDefault = () => new WeatherRangeConfig(),
        Json = WeatherFactJsonContext.Default,
        Label = WeatherFactsStr.WeatherFacts_Range_Label.Local(),
        Description = WeatherFactsStr.WeatherFacts_Range_Desc.Local(),
        Glyph = Glyphs.Weather,
        Keywords = ["range", "bar", "temperature", "high", "low", "forecast", "weather"],
        Sizing = _ => WidgetSizing.Range(new(2 * WidgetSizing.Floor, WidgetSizing.Floor), new(MetricRules.MinWidth, MetricRules.RowHeight), new(SizeStretch.Ceiling, SizeStretch.Ceiling)),
        Traits = WidgetTraits.None,
        Add = new(AddFolderId.WeatherFacts, 0, WeatherDayPartKinds.Rank + 4),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new WeatherRangeView(host);

    public void MenuLines(WidgetMenuLines menu, WidgetConfig config) => WeatherDayPartKinds.Lines(menu, config);
}

/// <summary>
/// [S-13919] A day part's reading: the background sync's answer at its place, as every weather atom follows it ([S-2663]);
/// a part draws it again when it comes and when its config does.
/// </summary>
internal sealed class DayPartFeed
{
    private readonly SkyFeed<WeatherInterest, CurrentWeather> _feed;

    public DayPartFeed(IWidgetHost host, DayPart part, Action render)
    {
        Config = WeatherDayPartKinds.Create(part);
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Reading, host.Weather.Show),
            () => WeatherInterests.For(Config),
            render);
    }

    public WeatherDayPartConfig Config { get; private set; }

    public CurrentWeather? Reading => _feed.Held?.Value;

    public string? Why => OnlineParts.Caption(_feed.Status);

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Apply(config);
        _feed.Attach(ticker, Config.TickInterval);
    }

    public void Apply(WidgetConfig config)
    {
        if (config is WeatherDayPartConfig part)
        {
            Config = part;
        }

        _feed.Cadence(Config.TickInterval);
        _feed.Reload();
    }

    public void Detach() => _feed.Detach();

    /// <summary>[S-11850] The sample day's reading at the sample place, nothing fetched.</summary>
    public void Sample()
    {
        var config = WeatherDayPartKinds.Create(Config.Part);
        config.Latitude = Themia.Sdk.Samples.SamplePlace.Latitude;
        config.Longitude = Themia.Sdk.Samples.SamplePlace.Longitude;
        config.LocationName = Themia.Sdk.Samples.SamplePlace.LocationName;
        config.Day = Config.Day;
        Config = config;
        _feed.Preview(new Fetched<CurrentWeather>(FetchStatus.Ok, Themia.Sdk.Samples.SampleWeather.Reading(), null, Themia.Sdk.Samples.SampleDay.MinutesAgo(4)), FetchStatus.Ok);
    }
}

/// <summary>[S-13919] A day's name, high or low on the one text face; "--" with no reading, why in its tooltip.</summary>
internal sealed partial class WeatherDayTextView : TextView, IWidgetSample
{
    private readonly DayPart _part;
    private readonly DayPartFeed _feed;

    public WeatherDayTextView(IWidgetHost host, TextKind kind, DayPart part)
        : base(host, kind)
    {
        _part = part;
        _feed = new DayPartFeed(host, part, Render);
    }

    protected override void OnAttach(Ticker ticker, WidgetConfig config) => _feed.Attach(ticker, config);

    protected override void OnApply(WidgetConfig config) => _feed.Apply(config);

    protected override void OnDetach() => _feed.Detach();

    public Task ShowSampleAsync()
    {
        _feed.Sample();
        return Task.CompletedTask;
    }

    private void Render()
        => Present(_feed.Reading is { } reading
            ? new TextShown(DayParts.Text(Strings.Current, _part, reading, _feed.Config.Day))
            : new TextShown(WeatherFactRules.Missing, Available: false) { Why = _feed.Why });
}

/// <summary>
/// [S-13920] A day's range bar: the family's bar (<see cref="ChartBar"/>: the track, the day's low to high on it in the accent)
/// along its box's long side (across; in a box taller than wide a column, the low at its foot), the inset at its ends, on the
/// forecast's scale; the track alone with no reading.
/// </summary>
internal sealed partial class WeatherRangeView : Grid, IWidgetView, IWidgetSample
{
    private readonly ChartBar _across = new() { VerticalAlignment = VerticalAlignment.Center, Margin = Family.SideInset };
    private readonly ChartBar _down = new(vertical: true) { Margin = new Thickness(0, FamilySpace.Inset, 0, FamilySpace.Inset) };
    private readonly DayPartFeed _feed;

    public WeatherRangeView(IWidgetHost host)
    {
        _feed = new DayPartFeed(host, DayPart.Range, Render);
        Children.Add(_across);
        Children.Add(_down);
        SizeChanged += (_, _) => Lay();
        Lay();
    }

    public void Attach(Ticker ticker, WidgetConfig config) => _feed.Attach(ticker, config);

    public void ApplyConfig(WidgetConfig config) => _feed.Apply(config);

    public void Detach() => _feed.Detach();

    public Task ShowSampleAsync()
    {
        _feed.Sample();
        return Task.CompletedTask;
    }

    private void Render()
    {
        var range = _feed.Reading is { } reading ? DayParts.Range(reading, _feed.Config.Day) : null;
        ToolTipService.SetToolTip(this, range is null ? _feed.Why : null);
        var (from, to) = range ?? (0, 0);
        _across.Show(to, from);
        _down.Show(to, from);
    }

    /// <summary>The bar along the box's long side.</summary>
    private void Lay()
    {
        var across = ActualWidth >= ActualHeight;
        _across.Visibility = across ? Visibility.Visible : Visibility.Collapsed;
        _down.Visibility = across ? Visibility.Collapsed : Visibility.Visible;
    }
}
