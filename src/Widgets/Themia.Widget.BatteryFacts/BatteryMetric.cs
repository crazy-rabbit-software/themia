using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.BatteryFacts;

/// <summary>
/// [S-14660] One kind of the battery facts family: one of what the battery itself reports as a single metric
/// (<see cref="MetricModule"/>), at home in its family (<see cref="AddFolderId.BatteryFacts"/>). SPEC.md beside this file.
/// </summary>
public abstract class BatteryMetricModule(string key)
    : MetricModule(BatteryKinds.Of(key), BatteryFactsJsonContext.Default, AddFolderId.BatteryFacts, BatteryKinds.Rank(key))
{
    /// <summary>[S-2023] A click on the card opens Windows' Power &amp; battery page.</summary>
    internal static CardOpen? Opens => CardOpen.Settings("batterysaver");

    public override FrameworkElement CreateView(IWidgetHost host)
        => new SystemMetricView(host, Kind, new BatteryMetricPart(Kind.Key));
}

/// <summary>[S-14660] What a battery fact card remembers: its trend; its fact is its kind's.</summary>
public abstract class BatteryMetricConfig(string key) : MetricConfig, IReadsSystem
{
    /// <summary>The fact its kind shows.</summary>
    [JsonIgnore]
    public string Key { get; } = key;

    /// <inheritdoc />
    protected override MetricHistory KindHistory => BatteryKinds.Of(Key).History;

    /// <summary>[S-2513] The charge and what the battery itself reports.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Battery, SystemSource.BatteryPack);

    public override WidgetConfig Clone()
    {
        var copy = (BatteryMetricConfig)BatteryKinds.Of(Key).Create();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>
/// [S-2423]..[S-2425] [S-14660] One of the battery's facts: its charge and status (the battery meter's own hub feed) and
/// what the battery reports through the battery class driver; a machine without a battery shows its "--" dimmed, why the
/// tooltip ([S-1942]). The device is asked only while Windows says there is a battery.
/// </summary>
internal sealed class BatteryMetricPart(string key) : SystemMetricPart
{
    private readonly WatchSlot<BatteryPower?> _power = new();
    private readonly WatchSlot<BatteryPack?> _pack = new();
    private ISystemFeeds? _hub;
    private ISystemMetricHost? _host;

    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => BatteryMetricModule.Opens;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, ISystemMetricHost host)
    {
        _hub = hub;
        _host = host;
        _power.Hold(hub, Sources.Battery, _ =>
        {
            NextReading();
            Show();
        });
        Show();
    }

    private void Show()
    {
        if (_host is not { } host || _hub is not { } hub || !_power.HasValue)
        {
            return;
        }

        if (_power.Value is not { } power)
        {
            _pack.Release();
            host.Present(new MetricShown(MetricValue.Unread) { Why = Str.Shell_Meter_Unavailable.Text() });
            return;
        }

        if (power.HasBattery)
        {
            // Read in the same pass when the battery first shows up ([S-2506]); presents again then.
            _pack.Hold(hub, Sources.BatteryPack, _ => Show());
        }
        else
        {
            _pack.Release();
        }

        host.Present(MetricRules.Pick(BatteryDetailRules.Sheet(Strings.Current, power, power.HasBattery && _pack.HasValue ? _pack.Value : null), key) with { Reading = LastReading });
    }

#if DEBUG
    /// <summary>[S-1942] A desktop's read on any machine.</summary>
    public override MetricShown? Absent()
        => MetricRules.Pick(BatteryDetailRules.Sheet(Strings.Current, new BatteryPower(false, null, true, false, null), null), key);
#endif

    public override void Detach()
    {
        _pack.Release();
        _power.Release();
        _hub = null;
        _host = null;
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BatteryStatusConfig))]
[JsonSerializable(typeof(BatteryTimeConfig))]
[JsonSerializable(typeof(BatteryHealthConfig))]
[JsonSerializable(typeof(BatteryFullCapacityConfig))]
[JsonSerializable(typeof(BatteryDesignCapacityConfig))]
[JsonSerializable(typeof(BatteryCyclesConfig))]
[JsonSerializable(typeof(BatteryRateConfig))]
[JsonSerializable(typeof(BatteryVoltageConfig))]
[JsonSerializable(typeof(BatteryTemperatureConfig))]
[JsonSerializable(typeof(BatteryChemistryConfig))]
[JsonSerializable(typeof(BatteryManufacturerConfig))]
internal sealed partial class BatteryFactsJsonContext : JsonSerializerContext;
