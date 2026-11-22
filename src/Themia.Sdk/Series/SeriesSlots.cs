using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Series;

/// <summary>
/// [S-15510] A setting of a widget that can take a series value: its type, how the value is read off a
/// config and written onto one, and the shared setting whose line shows it (so the host can show that line
/// as From repeater, [S-15512]). A kind exposes one by holding the contract the slot is over; nothing in a
/// module names the repeater.
/// </summary>
public abstract class SeriesSlot
{
    protected SeriesSlot(string key, SeriesType type, SharedSetting shared)
    {
        Key = key;
        Type = type;
        Shared = shared;
    }

    /// <summary>The slot's name, the same in every language: what a repeater's binds name it by.</summary>
    public string Key { get; }

    /// <summary>What kind of value it takes.</summary>
    public SeriesType Type { get; }

    /// <summary>The shared setting whose menu line is this slot's picker.</summary>
    public SharedSetting Shared { get; }

    /// <summary>Whether <paramref name="config"/> has the slot.</summary>
    public abstract bool Holds(WidgetConfig config);

    /// <summary>The value <paramref name="config"/> holds now as a token, or null where it holds none.</summary>
    public abstract string? Read(WidgetConfig config);

    /// <summary>Write the value <paramref name="token"/> onto <paramref name="config"/>; false (and nothing written) where it does not read.</summary>
    public abstract bool Write(WidgetConfig config, string token);

    /// <summary>
    /// [S-15520] Put <paramref name="config"/> in its own empty state for a row with no value (a day past any forecast, a
    /// drive that is not there); a slot with no such state (a place) leaves it as it is.
    /// </summary>
    public abstract void Blank(WidgetConfig config);

    public override string ToString() => Key;
}

/// <summary>[S-15510] A slot over the config contract <typeparamref name="T"/>.</summary>
public sealed class SeriesSlot<T> : SeriesSlot
    where T : class
{
    private readonly Func<T, string?> _read;
    private readonly Func<T, string, bool> _write;
    private readonly Func<T, bool>? _where;
    private readonly Action<T>? _blank;

    public SeriesSlot(string key, SeriesType type, SharedSetting shared, Func<T, string?> read, Func<T, string, bool> write, Func<T, bool>? where = null, Action<T>? blank = null)
        : base(key, type, shared)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _where = where;
        _blank = blank;
    }

    public override void Blank(WidgetConfig config)
    {
        if (Holds(config))
        {
            _blank?.Invoke((T)(object)config);
        }
    }

    public override bool Holds(WidgetConfig config) => config is T held && (_where?.Invoke(held) ?? true);

    public override string? Read(WidgetConfig config) => Holds(config) ? _read((T)(object)config) : null;

    public override bool Write(WidgetConfig config, string token)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Holds(config) && token is not null && SeriesTokens.Reads(Type, token) && _write((T)(object)config, token);
    }
}

/// <summary>
/// [S-15510] [S-15513] The registry of series slots, each over its contract: a drive (<see cref="IDriveChoice"/>,
/// and the disk atoms' <see cref="IDiskScope"/> as one drive), a place (<see cref="IPlaced"/>) and a day
/// (<see cref="IDayChoice"/>). A new type is a member here and of <see cref="SeriesType"/>, nowhere else.
/// </summary>
public static class SeriesSlots
{
    /// <summary>[S-15513] The drive of a drive space atom.</summary>
    public static SeriesSlot<IDriveChoice> Drive { get; } = new(
        "drive",
        SeriesType.Drive,
        SharedSettings.Drive,
        held => held.Drive ?? string.Empty,
        (held, token) =>
        {
            held.Drive = SeriesTokens.DriveOf(token)!;
            return true;
        },
        blank: held => held.Drive = SeriesTokens.NoDrive);

    /// <summary>[S-15513] The drive of a disk activity atom: one drive, never every drive together.</summary>
    public static SeriesSlot<IDiskScope> DiskDrive { get; } = new(
        "diskDrive",
        SeriesType.Drive,
        SharedSettings.DiskScope,
        held => held.Scope == DiskScope.OneDrive ? held.Drive ?? string.Empty : null,
        (held, token) =>
        {
            held.Scope = DiskScope.OneDrive;
            held.Drive = SeriesTokens.DriveOf(token)!;
            return true;
        },
        blank: held =>
        {
            held.Scope = DiskScope.OneDrive;
            held.Drive = SeriesTokens.NoDrive;
        });

    /// <summary>[S-15513] The place of a widget about one: the place chosen by hand, never the device's.</summary>
    public static SeriesSlot<IPlaced> Place { get; } = new(
        "place",
        SeriesType.Place,
        SharedSettings.Place,
        held => held.Latitude is { } lat && held.Longitude is { } lon && !held.UseDeviceLocation
            ? SeriesTokens.Place(new SeriesPlace(lat, lon, held.PlaceName ?? string.Empty))
            : null,
        (held, token) =>
        {
            var place = SeriesTokens.PlaceOf(token)!.Value;
            held.Latitude = place.Latitude;
            held.Longitude = place.Longitude;
            held.PlaceName = place.Name;
            held.UseDeviceLocation = false;
            return true;
        });

    /// <summary>[S-15513] The day of a day atom.</summary>
    public static SeriesSlot<IDayChoice> Day { get; } = new(
        "day",
        SeriesType.Day,
        SharedSettings.Day,
        held => SeriesTokens.Day(held.Day),
        (held, token) =>
        {
            held.Day = SeriesTokens.DayOf(token)!.Value;
            return true;
        },
        held => held.TakesDay,
        held => held.Day = DayRangeSource.Reach);

    /// <summary>Every slot.</summary>
    public static IReadOnlyList<SeriesSlot> All { get; } = [Drive, DiskDrive, Place, Day];

    /// <summary>The slots <paramref name="config"/> has, in registry order.</summary>
    public static IReadOnlyList<SeriesSlot> Of(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return [.. All.Where(s => s.Holds(config))];
    }

    /// <summary>The slots of <paramref name="type"/> that <paramref name="config"/> has.</summary>
    public static IReadOnlyList<SeriesSlot> Of(WidgetConfig config, SeriesType type)
        => [.. Of(config).Where(s => s.Type == type)];

    /// <summary>The slot named <paramref name="key"/>, or null.</summary>
    public static SeriesSlot? Find(string? key) => All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));
}
