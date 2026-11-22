using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Series;

/// <summary>
/// [S-15510] Where a repeater's values come from: an ordered list of tokens of one type, computed from the
/// repeater's own settings (a range, a picked list) or from data its owner keeps (the drives). A live source's
/// values change on their own; the host redraws its repeaters when they do. A source has a sentence for
/// having none ([S-15520]).
/// </summary>
public abstract class SeriesSource
{
    protected SeriesSource(string id, SeriesType type, Str name, Str none)
    {
        Id = id;
        Type = type;
        Name = name;
        None = none;
    }

    /// <summary>Its id, the same in every language: what <see cref="IRepeater.Values"/> stores.</summary>
    public string Id { get; }

    /// <summary>The type of its values.</summary>
    public SeriesType Type { get; }

    /// <summary>Its name on the repeater's Values line.</summary>
    public Str Name { get; }

    /// <summary>[S-15520] What a repeater says while the source has no value.</summary>
    public Str None { get; }

    /// <summary>Whether its values change without the repeater's settings changing (connected drives).</summary>
    public virtual bool Live => false;

    /// <summary>Whether it is a list the user picks (its values are <see cref="IRepeater.Picked"/>).</summary>
    public virtual bool Picks => false;

    /// <summary>Whether it is a range (<see cref="IRepeater.Start"/>, <see cref="IRepeater.Step"/>).</summary>
    public virtual bool Ranges => false;

    /// <summary>Its values now for <paramref name="repeater"/>, in order, each a token of <see cref="Type"/>.</summary>
    public abstract IReadOnlyList<string> Values(IRepeater repeater);

    public override string ToString() => Id;
}

/// <summary>[S-15513] A day range: the start day from today, then every step-th day, as far as a forecast reaches.</summary>
public sealed class DayRangeSource() : SeriesSource("dayRange", SeriesType.Day, Str.Series_Source_DayRange, Str.Series_None_Days)
{
    /// <summary>How many days ahead a forecast reaches: day 0 to day 13 (open-meteo's fourteen).</summary>
    public const int Reach = 14;

    public override bool Ranges => true;

    public override IReadOnlyList<string> Values(IRepeater repeater)
    {
        ArgumentNullException.ThrowIfNull(repeater);
        var step = Math.Max(1, repeater.Step);
        var values = new List<string>();
        for (var day = Math.Max(0, repeater.Start); day < Reach && (repeater.Count <= 0 || values.Count < repeater.Count); day += step)
        {
            values.Add(SeriesTokens.Day(day));
        }

        return values;
    }
}

/// <summary>[S-15513] Every drive the device has connected now, in the drives service's (letter) order; one with no medium, gone or unreachable is not connected.</summary>
public sealed class ConnectedDrivesSource() : SeriesSource("drivesConnected", SeriesType.Drive, Str.Series_Source_DrivesConnected, Str.Series_None_DrivesConnected)
{
    public override bool Live => true;

    public override IReadOnlyList<string> Values(IRepeater repeater) => Of(DriveList.NowOrNone());

    /// <summary>The roots of the connected ones among <paramref name="drives"/>, in order.</summary>
    public static IReadOnlyList<string> Of(IEnumerable<DriveEntry> drives)
        => [.. drives.Where(d => d.State is DriveState.Ready or DriveState.Pending).Select(d => d.Root).Distinct(StringComparer.OrdinalIgnoreCase)];
}

/// <summary>[S-15513] A list the user picks, its tokens in <see cref="IRepeater.Picked"/>: those that read as the type, each once.</summary>
public sealed class PickedSource(string id, SeriesType type, Str name, Str none) : SeriesSource(id, type, name, none)
{
    public override bool Picks => true;

    public override IReadOnlyList<string> Values(IRepeater repeater)
    {
        ArgumentNullException.ThrowIfNull(repeater);
        return [.. repeater.Picked.Where(t => SeriesTokens.Reads(Type, t)).Distinct(StringComparer.Ordinal)];
    }
}

/// <summary>
/// [S-15510] [S-15513] The registry of sources, each of one type; the first of a type is its default. A new
/// source is a member here, written by whoever owns its data, never in the repeater.
/// </summary>
public static class SeriesSources
{
    public static SeriesSource DayRange { get; } = new DayRangeSource();

    public static SeriesSource DrivesConnected { get; } = new ConnectedDrivesSource();

    public static SeriesSource DrivesPicked { get; } = new PickedSource("drivesPicked", SeriesType.Drive, Str.Series_Source_DrivesPicked, Str.Series_None_DrivesPicked);

    public static SeriesSource PlacesPicked { get; } = new PickedSource("placesPicked", SeriesType.Place, Str.Series_Source_PlacesPicked, Str.Series_None_PlacesPicked);

    /// <summary>Every source, each type's default first.</summary>
    public static IReadOnlyList<SeriesSource> All { get; } = [DayRange, DrivesConnected, DrivesPicked, PlacesPicked];

    /// <summary>The sources of <paramref name="type"/>, its default first.</summary>
    public static IReadOnlyList<SeriesSource> Of(SeriesType type) => [.. All.Where(s => s.Type == type)];

    /// <summary>The source named <paramref name="id"/>, or null.</summary>
    public static SeriesSource? Find(string? id) => All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The source <paramref name="repeater"/> reads: its Values when that names a source of its type, else the
    /// type's default; null while it repeats by nothing.
    /// </summary>
    public static SeriesSource? For(IRepeater repeater)
    {
        ArgumentNullException.ThrowIfNull(repeater);
        if (repeater.RepeatBy is not { } type)
        {
            return null;
        }

        return Find(repeater.Values) is { } chosen && chosen.Type == type ? chosen : Of(type)[0];
    }

    /// <summary>Its values now: none while it repeats by nothing.</summary>
    public static IReadOnlyList<string> ValuesOf(IRepeater repeater) => For(repeater)?.Values(repeater) ?? [];
}
