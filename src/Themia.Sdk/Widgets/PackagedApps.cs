using System.Xml;
using System.Xml.Linq;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-3960] Where a packaged (MSIX / Store) app's executable is found in the shell as the app Start
/// lists: <c>shell:AppsFolder\&lt;family&gt;!&lt;app id&gt;</c>.
/// <para>
/// A packaged app's executable lives under <c>...\WindowsApps\&lt;package full name&gt;\</c>, and its
/// pictures are the package's logos, not the file's icon resource: WhatsApp's
/// <c>WhatsApp.Root.exe</c> carries no icon at all, so asking the shell for the file's icon drew
/// Windows' blank program icon. The Apps-folder item is drawn from the package's own logo (the
/// unplated target-size asset where the package ships one), the picture Start shows.
/// </para>
/// <para>
/// The app is the <c>Application</c> in the package's <c>AppxManifest.xml</c> whose
/// <c>Executable</c> is this file, else the package's only app; never a guess among several. The
/// manifest is read through <c>readManifest</c> (its full path in, its text or null out), so the rule
/// is pure and the caller decides how a file is read.
/// </para>
/// </summary>
public static class PackagedApps
{
    /// <summary>The prefix of a shell parsing name for an app in the Apps folder.</summary>
    public const string AppsFolderPrefix = @"shell:AppsFolder\";

    /// <summary>A manifest is a few kilobytes; one this large is not one, and is not parsed.</summary>
    public const int MaxManifestCharacters = 1 << 20;

    /// <summary>
    /// The folder of the package an executable at <paramref name="path"/> belongs to, and the
    /// executable's path inside it; null for a path that is not under <c>WindowsApps\&lt;full name&gt;\</c>.
    /// </summary>
    public static (string Root, string Family, string Inside)? PackageOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parts = path.Trim().Replace('/', '\\').Split('\\');
        var at = Array.FindIndex(parts, part => part.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase));
        if (at < 0 || at + 2 >= parts.Length)
        {
            return null;
        }

        // <name>_<version>_<architecture>_<resource id>_<publisher id>: the family is name_publisher id.
        var fields = parts[at + 1].Split('_');
        if (fields.Length != 5 || fields[0].Length == 0 || fields[4].Length == 0 || fields[1].Length == 0)
        {
            return null;
        }

        var inside = string.Join('\\', parts[(at + 2)..]);
        if (inside.Length == 0 || parts[(at + 2)..].Any(part => part.Length == 0 || part is "." or ".."))
        {
            return null;
        }

        return (string.Join('\\', parts[..(at + 2)]), $"{fields[0]}_{fields[4]}", inside);
    }

    /// <summary>
    /// The <c>shell:AppsFolder\&lt;AUMID&gt;</c> name of the packaged app whose executable is
    /// <paramref name="path"/>, or null: not packaged, no manifest, a manifest that is not XML or does
    /// not say which app.
    /// </summary>
    public static string? AppsFolderTarget(string? path, Func<string, string?> readManifest)
    {
        ArgumentNullException.ThrowIfNull(readManifest);

        if (PackageOf(path) is not { } package)
        {
            return null;
        }

        if (readManifest(package.Root + @"\AppxManifest.xml") is not { Length: > 0 and <= MaxManifestCharacters } text)
        {
            return null;
        }

        if ((string?)AppFor(text, package.Inside)?.Attribute("Id") is not { } id)
        {
            return null;
        }

        return $"{AppsFolderPrefix}{package.Family}!{id}";
    }

    /// <summary>
    /// [S-3965] The package's own logo file for the app whose executable is <paramref name="path"/>,
    /// as it should be drawn on a card of <paramref name="theme"/> at <paramref name="pixels"/> per
    /// side; null when the app is not packaged, its manifest does not say, or no asset of the
    /// manifest's <c>Square44x44Logo</c> is in its folder (the caller then asks the shell, [S-3960]).
    /// <para>
    /// The shell's Apps-folder item draws the dark-theme (<c>altform-unplated</c>) asset whatever the
    /// card is, and a package whose logo is white for the dark taskbar (Codex) vanishes on a light
    /// card. So the asset is chosen here, by the resource qualifiers in its file name: on a light card
    /// the <c>altform-lightunplated</c> one (or <c>altform-unplated</c> with <c>theme-light</c>), on a
    /// dark card the <c>altform-unplated</c> one; failing that the plated logo, on the manifest's
    /// <c>BackgroundColor</c> (none when it is <c>transparent</c> or not <c>#RRGGBB</c>); failing that
    /// the other theme's unplated asset. High-contrast and other alternate forms are never picked.
    /// Within a kind, the smallest size not under <paramref name="pixels"/>, else the largest.
    /// <c>listFiles</c> takes a folder and gives the names of the files in it, or null.
    /// </para>
    /// </summary>
    public static PackagedLogo? Logo(
        string? path,
        Func<string, string?> readManifest,
        Func<string, IReadOnlyList<string>?> listFiles,
        LogoTheme theme,
        int pixels)
    {
        ArgumentNullException.ThrowIfNull(readManifest);
        ArgumentNullException.ThrowIfNull(listFiles);

        if (PackageOf(path) is not { } package)
        {
            return null;
        }

        if (readManifest(package.Root + @"\AppxManifest.xml") is not { Length: > 0 and <= MaxManifestCharacters } text)
        {
            return null;
        }

        var visuals = AppFor(text, package.Inside)?.Elements().FirstOrDefault(element => element.Name.LocalName == "VisualElements");
        if (visuals is null || RelativeAsset((string?)visuals.Attribute("Square44x44Logo")) is not { } logo)
        {
            return null;
        }

        var cut = logo.LastIndexOf('\\');
        var folder = cut < 0 ? package.Root : package.Root + @"\" + logo[..cut];
        var name = cut < 0 ? logo : logo[(cut + 1)..];
        var dot = name.LastIndexOf('.');
        if (dot <= 0 || listFiles(folder) is not { } files)
        {
            return null;
        }

        var stem = name[..dot];
        var extension = name[dot..];
        var assets = files.Select(file => Asset(file, stem, extension)).OfType<LogoAsset>().ToList();

        LogoKind[] order = theme == LogoTheme.Light
            ? [LogoKind.LightUnplated, LogoKind.Plated, LogoKind.Unplated]
            : [LogoKind.Unplated, LogoKind.Plated, LogoKind.LightUnplated];

        foreach (var kind in order)
        {
            var ofKind = assets.Where(asset => asset.Kind == kind).ToList();
            if (ofKind.Count == 0)
            {
                continue;
            }

            var best = ofKind.Where(asset => asset.Pixels >= pixels).MinBy(asset => asset.Pixels)
                ?? ofKind.MaxBy(asset => asset.Pixels)!;

            var plate = kind == LogoKind.Plated ? Plate((string?)visuals.Attribute("BackgroundColor")) : null;
            return new PackagedLogo(folder + @"\" + best.File, plate);
        }

        return null;
    }

    /// <summary>A manifest's asset path, made safe to join under the package folder; null when it climbs out or names a drive.</summary>
    private static string? RelativeAsset(string? asset)
    {
        if (string.IsNullOrWhiteSpace(asset))
        {
            return null;
        }

        var parts = asset.Trim().Replace('/', '\\').TrimStart('\\').Split('\\');
        if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.Contains(':')))
        {
            return null;
        }

        return string.Join('\\', parts);
    }

    /// <summary>
    /// One file as an asset of <paramref name="stem"/>: <c>stem.ext</c> (scale 100), or
    /// <c>stem.q1_q2.ext</c> with <c>targetsize-N</c>, <c>scale-N</c>, <c>altform-X</c>,
    /// <c>theme-X</c> and <c>contrast-standard</c> qualifiers. Null for another file, an unknown
    /// qualifier, a high-contrast asset or an alternate form other than the two unplated ones.
    /// </summary>
    private static LogoAsset? Asset(string file, string stem, string extension)
    {
        if (file.Length <= stem.Length + extension.Length - 1
            || !file.StartsWith(stem, StringComparison.OrdinalIgnoreCase)
            || !file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var middle = file[stem.Length..^extension.Length];
        if (middle.Length == 0)
        {
            return new LogoAsset(file, LogoKind.Plated, 44);
        }

        if (middle[0] != '.' || middle.Length == 1)
        {
            return null;
        }

        int? target = null;
        int? scale = null;
        string? altform = null;
        string? themeName = null;

        foreach (var qualifier in middle[1..].Split('_'))
        {
            var dash = qualifier.IndexOf('-');
            if (dash <= 0 || dash == qualifier.Length - 1)
            {
                return null;
            }

            var key = qualifier[..dash].ToLowerInvariant();
            var value = qualifier[(dash + 1)..].ToLowerInvariant();

            switch (key)
            {
                case "targetsize" when Number(value, 1024) is { } n:
                    target = n;
                    break;
                case "scale" when Number(value, 1000) is { } n:
                    scale = n;
                    break;
                case "altform":
                    altform = value;
                    break;
                case "theme":
                    themeName = value;
                    break;
                case "contrast" when value == "standard":
                    break;
                default:
                    return null;
            }
        }

        LogoKind? kind = (altform, themeName) switch
        {
            (null, null or "dark") => LogoKind.Plated,
            ("unplated", null or "dark") => LogoKind.Unplated,
            ("lightunplated", null or "light") => LogoKind.LightUnplated,
            ("unplated", "light") => LogoKind.LightUnplated,
            _ => null,
        };

        return kind is { } known ? new LogoAsset(file, known, target ?? ((scale ?? 100) * 44 / 100)) : null;
    }

    private static int? Number(string value, int max)
        => int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0 && n <= max ? n : null;

    /// <summary>A plate colour as <c>#RRGGBB</c>; null for <c>transparent</c>, a named colour or anything malformed.</summary>
    private static string? Plate(string? colour)
        => colour is { Length: 7 } && colour[0] == '#' && colour[1..].All(char.IsAsciiHexDigit) ? colour.ToUpperInvariant() : null;

    private enum LogoKind
    {
        Plated,
        Unplated,
        LightUnplated,
    }

    private sealed record LogoAsset(string File, LogoKind Kind, int Pixels);

    /// <summary>The manifest's app whose executable is <paramref name="inside"/>, else its only app; null otherwise.</summary>
    private static XElement? AppFor(string manifest, string inside)
    {
        XDocument document;

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = MaxManifestCharacters,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };

            using var reader = XmlReader.Create(new StringReader(manifest), settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException)
        {
            return null;
        }

        var apps = document.Descendants()
            .Where(element => element.Name.LocalName == "Application" && IsAppId((string?)element.Attribute("Id")))
            .ToList();

        var app = apps.FirstOrDefault(element => string.Equals(
                ((string?)element.Attribute("Executable"))?.Replace('/', '\\').TrimStart('\\'), inside, StringComparison.OrdinalIgnoreCase))
            ?? (apps.Count == 1 ? apps[0] : null);

        return app;
    }

    /// <summary>An app id as the manifest schema allows it: letters, digits and dots, starting with a letter, at most 64.</summary>
    private static bool IsAppId(string? id)
        => id is { Length: > 0 and <= 64 } && char.IsAsciiLetter(id[0]) && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '.');
}

/// <summary>
/// [S-3965] A packaged app's logo file, and the <c>#RRGGBB</c> plate it is drawn on (the manifest's
/// <c>BackgroundColor</c>), or null when it stands on the card as it is.
/// </summary>
public sealed record PackagedLogo(string File, string? Plate);
