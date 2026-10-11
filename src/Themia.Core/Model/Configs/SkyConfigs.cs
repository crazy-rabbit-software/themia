using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-3101] What the Sun and Moon widgets share: where the observer is. The astronomy runs offline
/// from these coordinates; the place name is only the header's title.
/// </summary>
public abstract class SkyPlaceConfig : WidgetConfig
{
    /// <summary>Zurich, the original's default.</summary>
    public const double DefaultLatitude = 47.37;

    public const double DefaultLongitude = 8.54;

    public const string DefaultPlace = "Zurich";

    /// <summary>A place name is a header title, not a document.</summary>
    public const int MaxPlaceLength = 80;

    /// <summary>Degrees north, -90 to 90.</summary>
    public double Latitude { get; set; } = DefaultLatitude;

    /// <summary>Degrees east, -180 to 180.</summary>
    public double Longitude { get; set; } = DefaultLongitude;

    /// <summary>The place name shown in the header.</summary>
    public string Place { get; set; } = DefaultPlace;

    /// <summary>
    /// A position in the sky is only interesting at minute granularity; the rest of the readout
    /// changes a few times a day.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    /// <summary>
    /// The coordinates actually used. Clamped rather than rejected as a second line behind
    /// <see cref="Repair"/>: the widget draws every tick and must never throw on a value in memory.
    /// </summary>
    [JsonIgnore]
    public double EffectiveLatitude => double.IsFinite(Latitude) ? Math.Clamp(Latitude, -90, 90) : DefaultLatitude;

    [JsonIgnore]
    public double EffectiveLongitude => double.IsFinite(Longitude) ? Math.Clamp(Longitude, -180, 180) : DefaultLongitude;

    /// <summary>
    /// [S-3101] A coordinate that is not a number in range would put the observer somewhere they
    /// never chose, so the pair goes back to the default place together, and says so.
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (!InRange(Latitude, 90) || !InRange(Longitude, 180))
        {
            repairs.Add($"{Kind} coordinates out of range; {DefaultPlace}");
            Latitude = DefaultLatitude;
            Longitude = DefaultLongitude;
            Place = DefaultPlace;
        }

        if (Place is null)
        {
            repairs.Add($"{Kind} place missing; {DefaultPlace}");
            Place = DefaultPlace;
        }
        else if (Place.Length > MaxPlaceLength)
        {
            repairs.Add($"{Kind} place longer than {MaxPlaceLength}; cut");
            Place = Place[..MaxPlaceLength];
        }

        RepairOwn(repairs);
        return repairs;
    }

    /// <summary>The derived widget's own repairs, appended to the shared ones.</summary>
    protected virtual void RepairOwn(List<string> repairs)
    {
    }

    /// <summary>Copy the shared fields onto <paramref name="target"/>.</summary>
    protected T CopyPlaceTo<T>(T target) where T : SkyPlaceConfig
    {
        target.Latitude = Latitude;
        target.Longitude = Longitude;
        target.Place = Place;
        return target;
    }

    private static bool InRange(double value, double limit) => double.IsFinite(value) && value >= -limit && value <= limit;
}

/// <summary>[S-3100] The sun's day where the observer is: its path, rise, set and daylight.</summary>
public sealed class SunConfig : SkyPlaceConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Sun;

    /// <summary>[S-170] How much of the sun's path is drawn.</summary>
    public SunPathMode PathMode { get; set; } = SunPathMode.Numbers;

    /// <summary>[S-170] Show the sunrise/sunset/daylight rows.</summary>
    public bool ShowEvents { get; set; } = true;

    protected override void RepairOwn(List<string> repairs)
    {
        if (!Enum.IsDefined(PathMode))
        {
            repairs.Add($"sun path mode {(int)PathMode} unknown; numbers");
            PathMode = SunPathMode.Numbers;
        }
    }

    public override WidgetConfig Clone() => CopyPlaceTo(new SunConfig
    {
        PathMode = PathMode,
        ShowEvents = ShowEvents,
    });
}

/// <summary>[S-3100] The moon where the observer is: its phase, rise and set.</summary>
public sealed class MoonConfig : SkyPlaceConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Moon;

    public override WidgetConfig Clone() => CopyPlaceTo(new MoonConfig());
}
