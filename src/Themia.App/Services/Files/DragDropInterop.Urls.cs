using System.Runtime.InteropServices;
using System.Text;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Widgets;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-1402] Reading what a browser or the Start menu drags: web addresses, and the shell items a
/// drag carries when it has no file paths. Added for the Shortcuts widget's drops; the rest of the
/// OLE interop is in <c>DragDropInterop.cs</c>.
/// </summary>
internal static unsafe partial class DragDropInterop
{
    private const ushort CfUnicodeText = 13;
    private const uint SigdnNormalDisplay = 0x00000000;
    private const uint SigdnFileSysPath = 0x80058000;
    private const uint SigdnDesktopAbsoluteParsing = 0x80028000;

    /// <summary>The most shell items read from one drag: a drop of thousands is not a set of shortcuts.</summary>
    private const int MaxShellItems = 64;

    private static ushort _urlFormat;

    /// <summary><c>UniformResourceLocatorW</c>, what a browser's address bar or tab offers.</summary>
    private static ushort UrlFormat => _urlFormat != 0 ? _urlFormat : _urlFormat = Register("UniformResourceLocatorW");

    /// <summary>
    /// [S-1402] The web addresses a data object carries: its <c>UniformResourceLocatorW</c>, else the
    /// http/https lines of its text. Only addresses the launch rule accepts; none when it has neither.
    /// </summary>
    public static IReadOnlyList<string> ReadUrls(nint data)
    {
        if (data == 0)
        {
            return [];
        }

        if (GetBytes(data, UrlFormat) is { } url && DropUrls.InText(Utf16(url)) is { Count: > 0 } fromUrl)
        {
            return fromUrl;
        }

        return GetBytes(data, CfUnicodeText) is { } text ? DropUrls.InText(Utf16(text)) : [];
    }

    /// <summary>A NUL-terminated UTF-16 buffer as a string (the buffer may run past the terminator).</summary>
    private static string Utf16(byte[] bytes)
    {
        var text = Encoding.Unicode.GetString(bytes);
        var end = text.IndexOf('\0', StringComparison.Ordinal);
        return end >= 0 ? text[..end] : text;
    }

    /// <summary>
    /// [S-1402] The shell items a data object carries (<c>SHCreateShellItemArrayFromDataObject</c>).
    /// A Start menu app arrives this way, with no <c>CF_HDROP</c>: its parsing name is an
    /// <c>AppsFolder</c> id and its display name is the name the Start menu shows. Empty when the
    /// object holds no shell items.
    /// </summary>
    public static IReadOnlyList<ShellItemInfo> ReadShellItems(nint data)
    {
        if (data == 0)
        {
            return [];
        }

        nint array;
        var iid = IidShellItemArray;

        if (SHCreateShellItemArrayFromDataObject(data, &iid, &array) < 0 || array == 0)
        {
            return [];
        }

        using var owned = new ComRef(array);
        uint count = 0;

        // IShellItemArray::GetCount is slot 7, GetItemAt slot 8.
        if (((delegate* unmanaged[Stdcall]<nint, uint*, int>)(*(nint**)array)[7])(array, &count) < 0)
        {
            return [];
        }

        var items = new List<ShellItemInfo>();

        for (uint i = 0; i < Math.Min(count, (uint)MaxShellItems); i++)
        {
            nint item;

            if (((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)(*(nint**)array)[8])(array, i, &item) < 0 || item == 0)
            {
                continue;
            }

            using var ownedItem = new ComRef(item);
            var parsing = DisplayName(item, SigdnDesktopAbsoluteParsing);
            var display = DisplayName(item, SigdnNormalDisplay);

            if (parsing is null && display is null)
            {
                continue;
            }

            items.Add(new ShellItemInfo(parsing ?? string.Empty, display ?? string.Empty, DisplayName(item, SigdnFileSysPath)));
        }

        if (count > MaxShellItems)
        {
            Log.Warn(Scope, $"a drag offered {count} shell items; read the first {MaxShellItems}");
        }

        return items;
    }

    /// <summary><c>IShellItem::GetDisplayName</c> (slot 5), or null when the item has no such name.</summary>
    private static string? DisplayName(nint item, uint form)
    {
        char* name = null;

        if (((delegate* unmanaged[Stdcall]<nint, uint, char**, int>)(*(nint**)item)[5])(item, form, &name) < 0 || name is null)
        {
            return null;
        }

        try
        {
            var text = new string(name);
            return text.Length == 0 ? null : text;
        }
        finally
        {
            CoTaskMemFree((nint)name);
        }
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHCreateShellItemArrayFromDataObject(nint data, Guid* iid, nint* array);
}
