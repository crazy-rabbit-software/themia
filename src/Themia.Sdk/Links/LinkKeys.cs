using Themia.Core.Widgets;

namespace Themia.Core.Links;

/// <summary>
/// [S-11931] [S-11941] [S-11946] [S-11951] [S-11956] The one way each kind of linked thing is keyed, so
/// two widgets that show the same app, person, file, drive or device publish and match the same value.
/// Every kind that takes part keys through these; none keys its own way.
/// </summary>
public static class LinkKeys
{
    /// <summary>
    /// [S-11931] An app by its executable: the lower-case stem of the path or file name
    /// (<c>C:\…\Code.exe</c> → <c>code</c>), as screen time records it. Null for no executable.
    /// </summary>
    public static string? App(string? executable)
        => AppNames.ExeStem(executable?.Trim()) is { } stem ? stem.ToLowerInvariant() : null;

    /// <summary>[S-11931] An app by a process name as Windows gives it (no extension): lower-cased. Null for none.</summary>
    public static string? AppOfProcess(string? processName)
        => string.IsNullOrWhiteSpace(processName) ? null : processName.Trim().ToLowerInvariant();

    /// <summary>[S-11931] The value an app is published as, or null for none.</summary>
    public static AppLink? AppLink(string? executable) => App(executable) is { } key ? new AppLink(key) : null;

    /// <summary>
    /// [S-11941] A person: the address lower-cased when there is one (an address is the person whatever
    /// name a list shows), else the name folded (trimmed, runs of space as one, lower-cased). Null for neither.
    /// </summary>
    public static string? Person(string? address, string? name = null)
        => Address(address) ?? Fold(name);

    /// <summary>[S-11941] The value a person is published as, or null for none.</summary>
    public static PersonLink? PersonLink(string? address, string? name = null)
        => Person(address, name) is { } key ? new PersonLink(key) : null;

    /// <summary>
    /// [S-11946] A file or folder: its full path, separators as Windows writes them, no trailing
    /// separator (a drive's root keeps its own), case-folded (Windows' names are not case-sensitive). Null for none.
    /// </summary>
    public static string? File(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var full = path.Trim().Replace('/', '\\');
        while (full.Length > 3 && full.EndsWith('\\'))
        {
            full = full[..^1];
        }

        return full.ToUpperInvariant();
    }

    /// <summary>[S-11946] The value a file is published as, or null for none.</summary>
    public static FileLink? FileLink(string? path) => File(path) is { } key ? new FileLink(key) : null;

    /// <summary>
    /// [S-11947] The file a widget names by a path, only where that path is a full one (<c>C:\…</c>, a share's
    /// <c>\\server\share\…</c>): a shell item (<c>::{…}</c>, <c>shell:…</c>), a web address or a bare name is
    /// no file and takes no part. Null for none.
    /// </summary>
    public static FileLink? LocalFileLink(string? path)
        => path is not null && path.Trim() is { Length: > 0 } text && System.IO.Path.IsPathFullyQualified(text) ? FileLink(text) : null;

    /// <summary>
    /// [S-11951] A drive: the letter and colon of a path on it, upper-case (<c>c:\Users</c> → <c>C:</c>).
    /// Null for a path not on a lettered drive (a share, nothing).
    /// </summary>
    public static string? Drive(string? path)
    {
        var text = path?.Trim();
        return text is { Length: >= 2 } && char.IsAsciiLetter(text[0]) && text[1] == ':'
            ? $"{char.ToUpperInvariant(text[0])}:"
            : null;
    }

    /// <summary>[S-11951] The value a drive is published as, or null for none.</summary>
    public static DriveLink? DriveLink(string? path) => Drive(path) is { } key ? new DriveLink(key) : null;

    /// <summary>
    /// [S-11956] A Bluetooth device: its address's twelve hex digits, upper-case, whatever separators it was
    /// written with (<c>a4:c1:38:0b:22:9f</c>, <c>A4C1380B229F</c>). Null for anything that is not one.
    /// </summary>
    public static string? Device(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var text = address.Trim();
        if (text.Any(c => !char.IsAsciiHexDigit(c) && c is not (':' or '-' or '.' or '_')))
        {
            return null;
        }

        var digits = new string([.. text.Where(char.IsAsciiHexDigit)]).ToUpperInvariant();
        return digits.Length == 12 ? digits : null;
    }

    /// <summary>[S-11956] A device address held as a number (Windows' <c>BluetoothAddress</c>); null for 0.</summary>
    public static string? Device(ulong address)
        => address is 0 or > 0xFFFF_FFFF_FFFF ? null : address.ToString("X12", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>[S-11956] The value a device is published as, or null for none.</summary>
    public static DeviceLink? DeviceLink(string? address) => Device(address) is { } key ? new DeviceLink(key) : null;

    /// <summary>
    /// [S-11957] A Bluetooth device by the id Windows gives its association endpoint
    /// (<c>Bluetooth#Bluetooth&lt;radio&gt;-&lt;device&gt;</c>, <c>BluetoothLE#BluetoothLE&lt;radio&gt;-&lt;device&gt;</c>):
    /// the device's own address, after the last dash. Any other id (a device container's, a game controller's,
    /// a node's) names no address and is null: a device is never guessed from its name.
    /// </summary>
    public static DeviceLink? DeviceOfEndpoint(string? id)
    {
        var text = id?.Trim();
        if (text is null || !text.StartsWith("Bluetooth", StringComparison.OrdinalIgnoreCase) || text.IndexOf('#') is var hash && hash < 0)
        {
            return null;
        }

        var dash = text.LastIndexOf('-');
        return dash > hash && dash < text.Length - 1 ? DeviceLink(text[(dash + 1)..]) : null;
    }

    private static string? Address(string? address)
    {
        var text = address?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // "Name <a@b>" carries its address inside.
        var open = text.LastIndexOf('<');
        var close = text.LastIndexOf('>');
        if (open >= 0 && close > open)
        {
            text = text[(open + 1)..close].Trim();
        }

        if (text.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            text = text[7..];
        }

        var at = text.IndexOf('@');
        return at > 0 && at < text.Length - 1 && !text.Contains(' ') ? text.ToLowerInvariant() : null;
    }

    private static string? Fold(string? name)
        => string.IsNullOrWhiteSpace(name)
            ? null
            : string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
