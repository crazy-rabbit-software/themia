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

        if (AppIdFor(text, package.Inside) is not { } id)
        {
            return null;
        }

        return $"{AppsFolderPrefix}{package.Family}!{id}";
    }

    /// <summary>The <c>Id</c> of the manifest's app whose executable is <paramref name="inside"/>, else its only app's; null otherwise.</summary>
    private static string? AppIdFor(string manifest, string inside)
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

        return (string?)app?.Attribute("Id");
    }

    /// <summary>An app id as the manifest schema allows it: letters, digits and dots, starting with a letter, at most 64.</summary>
    private static bool IsAppId(string? id)
        => id is { Length: > 0 and <= 64 } && char.IsAsciiLetter(id[0]) && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '.');
}
