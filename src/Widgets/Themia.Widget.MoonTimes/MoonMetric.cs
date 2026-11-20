using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.MoonTimes;

/// <summary>
/// [S-14695] One kind of the moon times family: one of the moon's times where the observer is, a single metric
/// (<see cref="MetricModule"/>), at home in its family under Weather (<see cref="AddFolderId.MoonTimes"/>). SPEC.md beside this file.
/// </summary>
public abstract class MoonMetricModule(string key)
    : MetricModule(MoonKinds.Of(key), MoonTimesJsonContext.Default, AddFolderId.MoonTimes, MoonKinds.Rank(key))
{
    /// <summary>[S-11127] The sky's shared place ([S-3101]).</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.AddRange(SkyPlaceRows.Lines(menu, (MoonMetricConfig)config));

    public override FrameworkElement CreateView(IWidgetHost host) => new MoonMetricView(host, Enum.Parse<MoonTimeKind>(Kind.Key));
}

/// <summary>[S-14695] What one moon time card remembers: its place (the sky's, [S-3101]); its time is its kind's.</summary>
public abstract class MoonMetricConfig(string key) : SkyPlaceConfig
{
    /// <summary>The time its kind shows.</summary>
    [JsonIgnore]
    public MoonTimeKind Time { get; } = Enum.Parse<MoonTimeKind>(key);

    public override WidgetConfig Clone() => CopyPlaceTo((MoonMetricConfig)MoonKinds.Of(Time.ToString()).Create());
}

/// <summary>
/// [S-14695] One of the moon's times on the single metric's face, from the SDK's astronomy (the moon reading's own): the
/// next moonrise or moonset ([S-3104], its day where it is not today, an em dash where there is none, [S-3106]) or the
/// date of the next full or new moon ([S-1491]), worked out again each minute.
/// </summary>
internal sealed partial class MoonMetricView : MetricView
{
    private readonly MoonTimeKind _time;
    private MoonMetricConfig _config;
    private IDisposable? _subscription;
    private DateTimeOffset _now = DateTimeOffset.Now;

    public MoonMetricView(IWidgetHost host, MoonTimeKind time)
        : base(host, MoonKinds.Of(time.ToString()).Glyph, MetricHistory.None)
    {
        _time = time;
        _config = (MoonMetricConfig)MoonKinds.Of(time.ToString()).Create();
    }

    protected override string MetricName => MoonTimeRules.Name(_time);

    protected override void OnAttach(Ticker ticker, WidgetConfig config)
    {
        _subscription?.Dispose();
        _subscription = ticker.Subscribe(_config.TickInterval, () =>
        {
            _now = DateTimeOffset.Now;
            Render();
        });
        OnApply(config);
    }

    protected override void OnApply(WidgetConfig config)
    {
        if (config is MoonMetricConfig own && own.Time == _time)
        {
            _config = own;
        }

        Render();
    }

    protected override void OnDetach()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private void Render()
    {
        var time = MoonTimeRules.For([_time], _now, _config.EffectiveLatitude, _config.EffectiveLongitude)[0];
        Present(new MetricShown(new MetricValue(time.Value)));
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MoonriseConfig))]
[JsonSerializable(typeof(MoonsetConfig))]
[JsonSerializable(typeof(NextFullMoonConfig))]
[JsonSerializable(typeof(NextNewMoonConfig))]
internal sealed partial class MoonTimesJsonContext : JsonSerializerContext;
