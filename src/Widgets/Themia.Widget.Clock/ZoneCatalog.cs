namespace Themia.Core.Widgets;

/// <summary>One IANA time zone and the Windows zone that keeps its rules.</summary>
/// <param name="IanaId">"Europe/Zurich".</param>
/// <param name="WindowsId">"W. Europe Standard Time": what <see cref="TimeZoneInfo"/> resolves on Windows.</param>
/// <param name="Listed">
/// False for an old name kept only so a stored id still resolves ("Asia/Calcutta" beside
/// "Asia/Kolkata") and for the bare <c>Etc/GMT±N</c> offsets: the picker offers each place once.
/// </param>
public readonly record struct ZoneEntry(string IanaId, string WindowsId, bool Listed)
{
    /// <summary>"Buenos Aires".</summary>
    public string City => ZoneCatalog.City(IanaId);

    /// <summary>"America"; empty for UTC.</summary>
    public string Region => ZoneCatalog.Region(IanaId);
}

/// <summary>
/// [S-1480] The clock's world-clock zones: IANA ids for the user (cities they can search for, and a
/// city to label the clock with), Windows ids for the arithmetic.
/// <para>
/// Why a table and not <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> on the IANA id: .NET
/// converts an IANA id to a Windows one through ICU, and the app builds with
/// <c>InvariantGlobalization</c> (no ICU), so every IANA lookup throws. Windows ids resolve from the
/// registry and always work, but "W. Europe Standard Time" names no city. The mapping is Windows'
/// own (<c>%windir%\Globalization\Time Zone\timezoneMapping.xml</c>, derived from CLDR), frozen
/// into <c>ZoneCatalog.Table.cs</c>; a test checks every Windows id in it resolves on this machine.
/// </para>
/// </summary>
public static partial class ZoneCatalog
{
    private static readonly ZoneEntry[] Table = BuildTable();

    private static readonly Dictionary<string, ZoneEntry> ById =
        Table.ToDictionary(entry => entry.IanaId, StringComparer.OrdinalIgnoreCase);

    /// <summary>The zones the picker offers, one per place, sorted by city.</summary>
    public static IReadOnlyList<ZoneEntry> Choices { get; } =
        [.. Table.Where(entry => entry.Listed).OrderBy(entry => entry.City, StringComparer.OrdinalIgnoreCase)];

    /// <summary>The entry for an IANA id (old names included), or null for anything the table lacks.</summary>
    public static ZoneEntry? Find(string? ianaId)
        => !string.IsNullOrWhiteSpace(ianaId) && ById.TryGetValue(ianaId.Trim(), out var entry) ? entry : null;

    /// <summary>
    /// The zone an IANA id runs on, or null when the id is unknown or this Windows lacks the zone
    /// (an older build without a zone added since). Never throws: the clock asks on every tick.
    /// </summary>
    public static TimeZoneInfo? Resolve(string? ianaId)
    {
        if (Find(ianaId) is not { } entry)
        {
            return null;
        }

        try
        {
            // Cached by the runtime after the first read, so the per-tick cost is a dictionary hit.
            return TimeZoneInfo.FindSystemTimeZoneById(entry.WindowsId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    /// <summary>"America/Argentina/Buenos_Aires" → "Buenos Aires"; "Etc/UTC" → "UTC".</summary>
    public static string City(string ianaId)
    {
        ArgumentNullException.ThrowIfNull(ianaId);

        if (ianaId.StartsWith("Etc/", StringComparison.OrdinalIgnoreCase))
        {
            return ianaId[4..];
        }

        var slash = ianaId.LastIndexOf('/');
        return ianaId[(slash + 1)..].Replace('_', ' ');
    }

    /// <summary>"America/Argentina/Buenos_Aires" → "America"; empty for <c>Etc/*</c> and bare ids.</summary>
    public static string Region(string ianaId)
    {
        ArgumentNullException.ThrowIfNull(ianaId);

        var slash = ianaId.IndexOf('/', StringComparison.Ordinal);
        var region = slash < 0 ? string.Empty : ianaId[..slash];
        return region.Equals("Etc", StringComparison.OrdinalIgnoreCase) ? string.Empty : region;
    }

    /// <summary>
    /// [S-1481] The picker's search: the listed zones whose city, region or id contains every word
    /// of <paramref name="query"/>, cities that <i>start</i> with it first. An empty query lists all.
    /// </summary>
    public static IReadOnlyList<ZoneEntry> Search(string? query, int max = int.MaxValue)
    {
        var words = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return [.. Choices.Take(max)];
        }

        static bool Has(string text, string word) => text.Contains(word, StringComparison.OrdinalIgnoreCase);

        return
        [
            .. Choices
                .Where(entry => words.All(word => Has(entry.City, word) || Has(entry.IanaId, word)))
                .OrderBy(entry => entry.City.StartsWith(words[0], StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .Take(max),
        ];
    }
}
