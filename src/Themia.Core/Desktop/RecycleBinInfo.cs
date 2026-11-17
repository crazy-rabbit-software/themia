using System.Buffers.Binary;
using System.Text;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-5597] One item in the Recycle Bin as the bin itself records it: where it was deleted from (its whole original
/// path) and when. <paramref name="Id"/> is how the host finds it again (the recycled copy's path, <c>$R…</c>).
/// </summary>
public sealed record RecycledItem(string Id, string OriginalPath, DateTimeOffset DeletedAt);

/// <summary>
/// [S-5597] The Recycle Bin's own record of a recycled item: the <c>$I…</c> file beside its <c>$R…</c> copy in
/// <c>$Recycle.Bin\&lt;SID&gt;</c>. Version 1 (Windows Vista to 8.1): the version, the size, the deletion time (a
/// FILETIME) and the original path in 260 UTF-16 characters; version 2 (Windows 10 and later): the same, the path's
/// length in characters (its null included) before the path. Read at a trust boundary: anything else is refused (null).
/// </summary>
public static class RecycleBinInfo
{
    /// <summary>[S-5597] The <c>$I</c> name for a recycled copy's <c>$R</c> path, or null where it is not one.</summary>
    public static string? InfoPathOf(string recycledPath)
    {
        ArgumentNullException.ThrowIfNull(recycledPath);
        var name = Path.GetFileName(recycledPath);
        if (name.Length < 3 || !name.StartsWith("$R", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var folder = Path.GetDirectoryName(recycledPath);
        return folder is null ? null : Path.Combine(folder, "$I" + name[2..]);
    }

    /// <summary>[S-5597] The original path and the deletion time a <c>$I</c> file holds; null where it is not one.</summary>
    public static (string OriginalPath, DateTimeOffset DeletedAt)? Parse(ReadOnlySpan<byte> info)
    {
        const int Header = 24;
        if (info.Length < Header)
        {
            return null;
        }

        var version = BinaryPrimitives.ReadInt64LittleEndian(info);
        var fileTime = BinaryPrimitives.ReadInt64LittleEndian(info[16..]);
        if (fileTime <= 0 || fileTime > DateTime.MaxValue.ToFileTimeUtc())
        {
            return null;
        }

        ReadOnlySpan<byte> path;
        switch (version)
        {
            case 1:
                if (info.Length < Header + 520)
                {
                    return null;
                }

                path = info.Slice(Header, 520);
                break;
            case 2:
                if (info.Length < Header + 4)
                {
                    return null;
                }

                var chars = BinaryPrimitives.ReadInt32LittleEndian(info[Header..]);
                if (chars <= 1 || chars > 32_768 || info.Length < Header + 4 + (chars * 2))
                {
                    return null;
                }

                path = info.Slice(Header + 4, chars * 2);
                break;
            default:
                return null;
        }

        var text = Encoding.Unicode.GetString(path);
        var end = text.IndexOf('\0', StringComparison.Ordinal);
        var original = end >= 0 ? text[..end] : text;
        if (original.Length < 3 || !Path.IsPathFullyQualified(original) || original.Any(char.IsControl))
        {
            return null;
        }

        return (original, new DateTimeOffset(DateTime.FromFileTimeUtc(fileTime), TimeSpan.Zero));
    }
}
