using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Services.SystemData;

/// <summary>
/// [S-2504] What a charted source's snapshot means on its meter: the reading and the point it adds to
/// one series (<c>instance</c>: a drive, an adapter; empty for the source's one series). The hub's
/// rules, not a machine's: this PC and the sample machine ([S-1931]) are drawn by the same ones.
/// </summary>
internal static class SeriesProjections
{
    /// <summary>[S-1140] Load across all cores.</summary>
    public static MeterSample Cpu(MeterReading snapshot, string instance) => MeterSample.Of(snapshot, ChartScale.Share);

    /// <summary>[S-1130] Physical memory in use.</summary>
    public static MeterSample Memory(MemorySnapshot? snapshot, string instance)
        => snapshot is { } s && MemoryFigures.UsedFraction(s) is { } fraction
            ? MeterSample.Of(new MeterReading(fraction, Formatting.Percent(fraction), true), ChartScale.Share)
            : new MeterSample(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()), null);

    /// <summary>[S-1162] [S-1820] Reads and writes per drive; the instance is a root or <see cref="Sources.AllDrives"/>.</summary>
    public static MeterSample Disk(DiskSnapshot snapshot, string instance)
    {
        var all = instance == Sources.AllDrives;
        var root = all ? SystemDrive.Root : instance;
        var sample = DiskRules.Read(all ? DiskScope.AllDrives : DiskScope.OneDrive, root, snapshot.Activity?.Rates);

        var reading = sample.State switch
        {
            // The fraction is only the "this is a sample" mark: a window-scaled chart plots the value.
            DiskReadingState.Rate => new MeterReading(
                ChartScale.Fraction(sample.Rates.Total, DiskRules.ChartFloor),
                Formatting.Rate(sample.Rates.Total),
                true,
                Value: sample.Rates.Total),

            // The counters' first collection: no rate yet, so no number and no point on the chart.
            DiskReadingState.Waiting => new MeterReading(null, "--", false),
            DiskReadingState.DriveMissing => MeterReading.Unavailable(Str.WidgetsB_Disk_Unavailable.Format(("drive", DiskRules.Instance(root)))),
            _ => MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()),
        };

        return MeterSample.Of(reading, DiskRules.Scale);
    }

    /// <summary>[S-1190] [S-1191] Download (filled) and upload.</summary>
    public static MeterSample Network(ThroughputSample? snapshot, string instance)
    {
        if (snapshot is null)
        {
            return new MeterSample(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()), null);
        }

        if (snapshot.Interfaces.Count == 0)
        {
            return new MeterSample(MeterReading.Unavailable(Str.WidgetsB_Network_NoConnection.Text()), null);
        }

        // The fraction only marks a sample: the chart is window-scaled and plots the values.
        var reading = new MeterReading(
            ChartScale.Fraction(snapshot.Down, Sources.NetworkChartFloor),
            Formatting.Rate(snapshot.Down),
            true,
            Value: snapshot.Down,
            Second: snapshot.Up);

        return MeterSample.Of(reading, ChartScale.Peak(Sources.NetworkChartFloor));
    }

    /// <summary>[S-1180] [S-1187] The charge, and whether it is charging.</summary>
    public static MeterSample Battery(BatteryPower? snapshot, string instance)
    {
        if (snapshot is not { } power)
        {
            return new MeterSample(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()), null);
        }

        if (!power.HasBattery)
        {
            return new MeterSample(MeterReading.Missing(Str.WidgetsB_Battery_None.Text()), null);
        }

        if (power.Percent is not { } percent)
        {
            return new MeterSample(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()), null);
        }

        var reading = new MeterReading(percent / 100.0, LocaleFormat.Percent(Strings.Current, percent), true, Charging: power.Charging);
        return MeterSample.Of(reading, ChartScale.Share);
    }

    /// <summary>[S-431] One adapter's load; the instance is its DXGI position.</summary>
    public static MeterSample Gpu(GpuSnapshot snapshot, string instance)
    {
        if (!int.TryParse(instance, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index)
            || index >= snapshot.Readings.Count)
        {
            // [S-433] An adapter that has gone (undocked, or not on this machine).
            return new MeterSample(new MeterReading(null, "--", false), null);
        }

        var reading = snapshot.Readings[index];

        // [S-430] Load is a rate: the first sample has none, which is "--", never a 0% that did not happen.
        if (!reading.HasLoad)
        {
            return new MeterSample(new MeterReading(null, "--", false), null);
        }

        var fraction = Math.Clamp(reading.Utilization / 100, 0, 1);
        return MeterSample.Of(new MeterReading(fraction, Formatting.Percent(fraction), true, Value: reading.Utilization), ChartScale.Share);
    }

    /// <summary>[S-1170] [S-1172] An answer is its latency; an unanswered probe a dimmed "--" that plots on the baseline.</summary>
    public static MeterSample Ping(PingOutcome snapshot, string instance)
    {
        var reading = snapshot.Reading is { Available: true, Value: { } ms }
            ? new MeterReading(Math.Clamp(ms / PingStats.LatencyCeilingMs, 0, 1), Formatting.Latency(ms), true, Value: ms)
            : new MeterReading(0, "--", false, Value: 0, Reason: snapshot.Reading.Reason ?? PingStats.StatusText(PingStatus.Offline));

        return MeterSample.Of(reading, ChartScale.Peak(1));
    }
}
