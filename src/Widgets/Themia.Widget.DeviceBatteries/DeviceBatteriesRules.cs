using Themia.Sdk.Devices;

namespace Themia.Widget.DeviceBatteries;

/// <summary>[S-10001] One device's row: its battery after the fold.</summary>
/// <param name="Name">One line, at most 64 characters; empty where Windows names none (the spec's "Unknown device").</param>
public sealed record DeviceBatteryRow(string Id, string Name, DeviceKind Kind, int Percent, bool? Charging, DeviceBatterySource Source);

/// <summary>
/// [S-10001] [S-10002] [S-10003] [S-10004] The rules the list is drawn by: one row per device, folded
/// across the paths Windows reports it on, lowest battery first. Pure: the reading needs Windows, these
/// do not.
/// </summary>
public static class DeviceBatteriesRules
{
    /// <summary>[S-10002] The longest name a row keeps.</summary>
    public const int MaxName = 64;

    /// <summary>[S-10002] A name off a device's radio or driver as one line of at most <see cref="MaxName"/> characters; blank is empty.</summary>
    public static string DisplayName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var collapsed = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return collapsed.Length <= MaxName ? collapsed : collapsed[..MaxName];
    }

    /// <summary>[S-10003] Whether a level is low: at or below the threshold.</summary>
    public static bool IsLow(int percent, int threshold) => percent <= threshold;

    /// <summary>
    /// [S-10002] Whether two readings are one device: the same container, or, where at least one has
    /// none, the same name (ignoring case; a nameless device is never folded). Two containers that
    /// differ are two devices, whatever they are called: two "Xbox Wireless Controller"s stay two rows.
    /// </summary>
    public static bool SameDevice(DeviceBatteryRow a, Guid? aContainer, DeviceBatteryRow b, Guid? bContainer)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (aContainer is { } x && bContainer is { } y)
        {
            return x == y;
        }

        return a.Name.Length > 0 && a.Name.Equals(b.Name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// [S-10001] [S-10002] [S-10004] The rows: every reading folded into its device (the most trusted
    /// source's level wins, <see cref="DeviceBatterySource"/>; the charging state, the kind and the
    /// name fill in from the others where it has none), a charging device left out unless
    /// <paramref name="showCharging"/>, ordered lowest battery first (then by name, then by id, so two
    /// identical reads give one order), at most <paramref name="max"/>.
    /// </summary>
    public static List<DeviceBatteryRow> Arrange(IEnumerable<DeviceBatteryReading> readings, bool showCharging, int max)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var rows = new List<(DeviceBatteryRow Row, Guid? Container)>();

        foreach (var reading in readings
            .Where(r => r.Percent is >= 0 and <= 100)
            .OrderBy(r => r.Source)
            .ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            var row = new DeviceBatteryRow(reading.Id, DisplayName(reading.Name), reading.Kind, reading.Percent, reading.Charging, reading.Source);
            var index = rows.FindIndex(known => SameDevice(known.Row, known.Container, row, reading.Container));

            if (index < 0)
            {
                rows.Add((row, reading.Container));
                continue;
            }

            var (kept, container) = rows[index];
            rows[index] = (kept with
            {
                Name = kept.Name.Length > 0 ? kept.Name : row.Name,
                Kind = kept.Kind != DeviceKind.Unknown ? kept.Kind : row.Kind,
                Charging = kept.Charging ?? row.Charging,
            }, container ?? reading.Container);
        }

        return
        [
            .. rows
                .Select(r => r.Row)
                .Where(row => showCharging || row.Charging != true)
                .OrderBy(row => row.Percent)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Id, StringComparer.Ordinal)
                .Take(Math.Max(1, max)),
        ];
    }
}
