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

namespace Themia.Widget.SunTimes;

/// <summary>
/// [S-14690] One kind of the sun times family: one of the sun's times where the observer is, a single metric
/// (<see cref="MetricModule"/>), at home in its family under Weather (<see cref="AddFolderId.SunTimes"/>). SPEC.md beside this file.
/// </summary>
public abstract class SunMetricModule(string key)
    : MetricModule(SunKinds.Of(key), SunTimesJsonContext.Default, AddFolderId.SunTimes, SunKinds.Rank(key))
{
    /// <summary>[S-11117] The sky's shared place ([S-3101]).</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.AddRange(SkyPlaceRows.Lines(menu, (SunMetricConfig)config));

    public override FrameworkElement CreateView(IWidgetHost host) => new SunMetricView(host, Enum.Parse<SunTimeKind>(Kind.Key));
}

/// <summary>[S-14690] What one sun time card remembers: its place (the sky's, [S-3101]; a new one Zurich); its time is its kind's.</summary>
public abstract class SunMetricConfig(string key) : SkyPlaceConfig
{
    /// <summary>The time its kind shows.</summary>
    [JsonIgnore]
    public SunTimeKind Time { get; } = Enum.Parse<SunTimeKind>(key);

    public override WidgetConfig Clone() => CopyPlaceTo((SunMetricConfig)SunKinds.Of(Time.ToString()).Create());
}

/// <summary>
/// [S-14690] One of the sun's times on the single metric's face, from the SDK's astronomy (the sun graphic's own, so the
/// two agree to the minute), worked out again each minute: a rise or set that does not happen (inside the polar circles)
/// is the em dash ([S-161]).
/// </summary>
internal sealed partial class SunMetricView : MetricView
{
    private readonly SunTimeKind _time;
    private SunMetricConfig _config;
    private IDisposable? _subscription;
    private DateTimeOffset _now = DateTimeOffset.Now;

    public SunMetricView(IWidgetHost host, SunTimeKind time)
        : base(host, SunKinds.Of(time.ToString()).Glyph, MetricHistory.None)
    {
        _time = time;
        _config = (SunMetricConfig)SunKinds.Of(time.ToString()).Create();
    }

    protected override string MetricName => SunTimeRules.Name(_time);

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
        if (config is SunMetricConfig own && own.Time == _time)
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
        var time = SunTimeRules.For([_time], _now, _config.EffectiveLatitude, _config.EffectiveLongitude)[0];
        Present(new MetricShown(new MetricValue(time.Value)));
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SunriseConfig))]
[JsonSerializable(typeof(SunsetConfig))]
[JsonSerializable(typeof(DaylightConfig))]
[JsonSerializable(typeof(SolarNoonConfig))]
[JsonSerializable(typeof(DawnConfig))]
[JsonSerializable(typeof(DuskConfig))]
internal sealed partial class SunTimesJsonContext : JsonSerializerContext;
