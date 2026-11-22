using System.Globalization;

namespace Themia.Core.Series;

/// <summary>A place carried as a series value: where it is and what it is called.</summary>
public readonly record struct SeriesPlace(double Latitude, double Longitude, string Name);

/// <summary>
/// [S-15510] How a value is written as a token and read back: a day is its offset in invariant digits,
/// a drive its root, a place <c>latitude|longitude|name</c> in invariant numbers. A token that does not
/// read is no value (null), never a guess.
/// </summary>
public static class SeriesTokens
{
    private const char Bar = '|';

    /// <summary>A day's token: its offset from today.</summary>
    public static string Day(int offset) => offset.ToString(CultureInfo.InvariantCulture);

    /// <summary>A day token's offset, or null when it is not a whole number from 0.</summary>
    public static int? DayOf(string? token)
        => int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var day) && day >= 0 ? day : null;

    /// <summary>A place's token.</summary>
    public static string Place(SeriesPlace place)
        => string.Create(CultureInfo.InvariantCulture, $"{place.Latitude:R}{Bar}{place.Longitude:R}{Bar}{place.Name}");

    /// <summary>A place token read back, or null when its coordinates do not read or lie off the Earth.</summary>
    public static SeriesPlace? PlaceOf(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var parts = token.Split(Bar, 3);
        if (parts.Length < 3
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
            || !double.IsFinite(lat) || !double.IsFinite(lon) || Math.Abs(lat) > 90 || Math.Abs(lon) > 180)
        {
            return null;
        }

        return new SeriesPlace(lat, lon, parts[2]);
    }

    /// <summary>[S-15520] A root no drive has: a drive atom given it shows its own state for a drive that is not there.</summary>
    public const string NoDrive = @"?:\";

    /// <summary>A drive token: its root as the drive widgets store it (<c>C:\</c>), or null for no root.</summary>
    public static string? DriveOf(string? token)
        => string.IsNullOrWhiteSpace(token) ? null : token.Trim();

    /// <summary>Whether <paramref name="token"/> reads as a value of <paramref name="type"/>.</summary>
    public static bool Reads(SeriesType type, string? token) => type switch
    {
        SeriesType.Day => DayOf(token) is not null,
        SeriesType.Drive => DriveOf(token) is not null,
        SeriesType.Place => PlaceOf(token) is not null,
        _ => false,
    };
}
