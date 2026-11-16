namespace Themia.App.Services.Files;

/// <summary>
/// [S-5559] The icon of a kind of file, named without a file: what a menu line that makes one (New ›
/// Folder, Shortcut, a template's "Text Document") shows before anything exists. A line carries the name
/// as its icon file; the host's icon reader tells it from a path by its prefix (no path starts with it)
/// and asks the shell for the registered type's icon by the extension alone, touching no file.
/// </summary>
public static class TypeIconName
{
    /// <summary>What every name here starts with; no file-system path or <c>shell:</c> name does.</summary>
    public const string Prefix = "type:";

    /// <summary>The folder's icon.</summary>
    public const string Folder = Prefix + "folder";

    /// <summary>The icon of a shortcut that points at nothing yet.</summary>
    public const string Shortcut = Prefix + ".lnk";

    /// <summary>
    /// The icon of the files with <paramref name="extension"/> (".txt"; the dot is added when missing);
    /// null for none or one that is no extension (a separator, a blank, a wildcard), so nothing odd is
    /// ever handed to the shell as a file name.
    /// </summary>
    public static string? Of(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var ext = extension.Trim();
        if (ext[0] != '.')
        {
            ext = "." + ext;
        }

        if (ext.Length < 2 || ext.Length > 64 || ext.AsSpan(1).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || ext.AsSpan(1).IndexOfAny(" .*?") >= 0)
        {
            return null;
        }

        return Prefix + ext.ToLowerInvariant();
    }

    /// <summary>
    /// What <paramref name="name"/> names: the folder, or an extension (with its dot); null when it is
    /// not one of these names (a path, a <c>shell:</c> name, anything malformed).
    /// </summary>
    public static (bool Folder, string Extension)? Parse(string? name)
    {
        if (name is null || !name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(name, Folder, StringComparison.Ordinal))
        {
            return (true, string.Empty);
        }

        var rest = name[Prefix.Length..];
        return string.Equals(Of(rest), name, StringComparison.Ordinal) ? (false, rest) : null;
    }
}
