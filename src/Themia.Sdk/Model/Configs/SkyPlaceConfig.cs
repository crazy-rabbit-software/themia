using System.Text.Json.Serialization;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-3101] What the Sun and Moon widgets share: where the observer is. The astronomy runs offline
/// from these coordinates; the place name is only the header's title.
/// <para>
/// [S-2848] A new tile starts at Zurich; a cleared place is null coordinates, written as null (not
/// left out, which would read back as Zurich), and the tile waits for setup like a weather widget.
/// </para>
/// </summary>
public abstract class SkyPlaceConfig : WidgetConfig, IPlaced
{
    /// <summary>Zurich, the original's default.</summary>
    public const double DefaultLatitude = 47.37;

    public const double DefaultLongitude = 8.54;

    public const string DefaultPlace = "Zurich";

    /// <summary>A place name is a header title, not a document.</summary>
    public const int MaxPlaceLength = 80;

    /// <summary>Degrees north, -90 to 90, or null once the place is cleared ([S-2848]).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public double? Latitude { get; set; } = DefaultLatitude;

    /// <summary>Degrees east, -180 to 180, or null once the place is cleared ([S-2848]).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public double? Longitude { get; set; } = DefaultLongitude;

    /// <summary>The place name shown in the header.</summary>
    public string Place { get; set; } = DefaultPlace;

    /// <summary>[S-2848] Whether a place is chosen. Zero is a real coordinate, so a null test.</summary>
    [JsonIgnore]
    public bool HasLocation => Latitude is { } lat
        && Longitude is { } lon
        && double.IsFinite(lat)
        && double.IsFinite(lon);

    /// <summary>[S-2848] No place: the tile waits for setup until one is picked or typed.</summary>
    public void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
        Place = string.Empty;
    }

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
    public double EffectiveLatitude => Latitude is { } lat && double.IsFinite(lat) ? Math.Clamp(lat, -90, 90) : DefaultLatitude;

    [JsonIgnore]
    public double EffectiveLongitude => Longitude is { } lon && double.IsFinite(lon) ? Math.Clamp(lon, -180, 180) : DefaultLongitude;

    /// <summary>
    /// [S-3101] A coordinate that is not a number in range would put the observer somewhere they
    /// never chose, so the pair goes back to the default place together, and says so. A null
    /// coordinate is no place ([S-2848]), not a bad one: it is left for setup.
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if ((Latitude is { } lat && !InRange(lat, 90)) || (Longitude is { } lon && !InRange(lon, 180)))
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
