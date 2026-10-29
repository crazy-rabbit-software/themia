using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Diagnostics;
using Themia.Sdk;

namespace Themia.App.Services;

/// <summary>
/// [S-5512] A desktop item's icon as Explorer's desktop draws it: the system image list's image with
/// the item's overlay (the shortcut arrow, a share, a sync engine's state) composed by the image list
/// itself. <c>IShellItemImageFactory</c> (the rest of this class) draws no overlay, so the desktop's
/// items come here. One extraction at a time with every other icon ([S-1324]).
/// </summary>
internal static partial class AppIcons
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSysIconIndex = 0x000004000;
    private const uint ShgfiPidl = 0x000000008;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint ShgfiOverlayIndex = 0x000000040;
    private const uint FileAttributeDirectory = 0x10;
    private const uint FileAttributeNormal = 0x80;
    private const int ShilExtraLarge = 2;
    private const int ShilJumbo = 4;
    private const uint IldTransparent = 0x1;
    private const int ShortcutOverlay = 2;
    private const int MaxOverlay = 15;
    private const int SlotGetIcon = 10; // IImageList: IUnknown (3) + Add .. Remove (7)

    /// <summary>IID_IImageList.</summary>
    private static readonly Guid ImageListIid = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    /// <summary>
    /// [S-5512] The icon of the desktop item <paramref name="key"/> at <paramref name="size"/> (physical)
    /// pixels, overlay included; null when there is none (logged). A key the shell cannot parse gets
    /// its type's icon from <paramref name="name"/> (and the arrow when <paramref name="kind"/> is a shortcut).
    /// </summary>
    public static async Task<ImageSource?> LoadOverlaidAsync(string key, string name, DesktopItemKind kind, int size)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        try
        {
            // Back on the caller's (UI) thread for the bitmap.
            var pixels = await ExtractOverlaidSerialisedAsync(key, name, kind, Math.Clamp(size, 16, 256)).ConfigureAwait(true);
            return pixels is null ? null : ToBitmap(pixels);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not extract a desktop item's icon", ex);
            return null;
        }
    }

    private static async Task<Pixels?> ExtractOverlaidSerialisedAsync(string key, string name, DesktopItemKind kind, int size)
    {
        await OneAtATime.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => ExtractOverlaid(key, name, kind, size)).ConfigureAwait(false);
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private static unsafe Pixels? ExtractOverlaid(string key, string name, DesktopItemKind kind, int size)
    {
        var initialised = CoInitializeEx(0, CoInitMultithreaded);
        nint list = 0;
        nint icon = 0;
        nint pidl = 0;

        try
        {
            var info = default(OverlaidFileInfo);
            uint attributesOut = 0;

            // The icon handle is only there because the overlay index needs it; it is destroyed below.
            var found = SHParseDisplayName(key, 0, &pidl, 0, &attributesOut) >= 0 && pidl != 0
                && SHGetFileInfoW((char*)pidl, 0, &info, (uint)sizeof(OverlaidFileInfo), ShgfiPidl | ShgfiIcon | ShgfiSysIconIndex | ShgfiOverlayIndex) != 0;

            if (!found)
            {
                // Not (or no longer) there: its type's icon, from the key's own file name (a shortcut keeps its .lnk).
                info = default;
                var typed = DesktopItemKeys.IsShellItem(key) ? name : Path.GetFileName(key.TrimEnd(Path.DirectorySeparatorChar));
                fixed (char* path = typed)
                {
                    var attributes = kind == DesktopItemKind.Folder ? FileAttributeDirectory : FileAttributeNormal;
                    if (SHGetFileInfoW(path, attributes, &info, (uint)sizeof(OverlaidFileInfo), ShgfiUseFileAttributes | ShgfiSysIconIndex) == 0)
                    {
                        return null;
                    }
                }
            }

            if (info.Icon != 0)
            {
                DestroyIcon(info.Icon);
            }

            var index = info.IconIndex & 0x00FFFFFF;
            var overlay = (info.IconIndex >> 24) & 0xFF;
            // The image list's overlays are numbered 1..15; the shell's own: 1 the share, 2 the shortcut
            // arrow. A shortcut the shell gave no overlay for (one not on disk) gets the arrow.
            if (overlay == 0 && kind == DesktopItemKind.Shortcut)
            {
                overlay = ShortcutOverlay;
            }

            if (overlay is < 0 or > MaxOverlay)
            {
                overlay = 0;
            }

            // [S-5563] The list that has the pixels: extra-large up to 48, jumbo above.
            var jumbo = DesktopIconPixels.ListFor(size) == DesktopIconList.Jumbo;
            var drawn = FromList(jumbo ? ShilJumbo : ShilExtraLarge, index, overlay, ref list, ref icon);

            // An item with no large image comes back from the jumbo list as its 48-pixel image in a
            // corner of an empty square: the extra-large image, drawn up, is the icon then.
            if (jumbo && drawn is not null && DesktopIconPixels.HoldsOnlySmallImage(drawn.Bgra, drawn.Width, drawn.Height))
            {
                drawn = FromList(ShilExtraLarge, index, overlay, ref list, ref icon) ?? drawn;
            }

            return drawn;
        }
        finally
        {
            if (icon != 0)
            {
                DestroyIcon(icon);
            }

            if (list != 0)
            {
                ((delegate* unmanaged[Stdcall]<nint, uint>)(*(nint**)list)[2])(list);
            }

            if (pidl != 0)
            {
                CoTaskMemFree(pidl);
            }

            if (initialised >= 0)
            {
                CoUninitialize();
            }
        }
    }

    /// <summary>
    /// [S-5512] One image of a system image list with the overlay composed by the list. The list and
    /// icon handles of an earlier call are released first; the caller releases the last ones.
    /// </summary>
    private static unsafe Pixels? FromList(int which, int index, int overlay, ref nint list, ref nint icon)
    {
        if (icon != 0)
        {
            DestroyIcon(icon);
            icon = 0;
        }

        if (list != 0)
        {
            ((delegate* unmanaged[Stdcall]<nint, uint>)(*(nint**)list)[2])(list);
            list = 0;
        }

        var iid = ImageListIid;
        nint got;
        if (SHGetImageList(which, &iid, &got) < 0 || got == 0)
        {
            return null;
        }

        list = got;

        // INDEXTOOVERLAYMASK: the overlay's one-based number in the flags' second byte.
        nint drawn;
        var getIcon = (delegate* unmanaged[Stdcall]<nint, int, uint, nint*, int>)(*(nint**)got)[SlotGetIcon];
        if (getIcon(got, index, IldTransparent | (uint)(overlay << 8), &drawn) < 0 || drawn == 0)
        {
            return null;
        }

        icon = drawn;
        return ReadIcon(drawn);
    }

    /// <summary>An icon's pixels, premultiplied: its own alpha when it has one, else its mask.</summary>
    private static unsafe Pixels? ReadIcon(nint icon)
    {
        if (!GetIconInfo(icon, out var parts))
        {
            return null;
        }

        try
        {
            if (parts.Color == 0)
            {
                return null;
            }

            var pixels = Read(parts.Color);
            if (pixels is null || parts.Mask == 0 || !Opaque(pixels))
            {
                return pixels;
            }

            // No alpha channel: Read made it opaque; the mask says what is transparent.
            var memory = CreateCompatibleDC(0);
            if (memory == 0)
            {
                return pixels;
            }

            try
            {
                var mask = new byte[pixels.Width * pixels.Height * 4];
                if (!Bits(memory, parts.Mask, pixels.Width, pixels.Height, mask))
                {
                    return pixels;
                }

                for (var i = 0; i < mask.Length; i += 4)
                {
                    if (mask[i] != 0 || mask[i + 1] != 0 || mask[i + 2] != 0)
                    {
                        pixels.Bgra[i] = pixels.Bgra[i + 1] = pixels.Bgra[i + 2] = pixels.Bgra[i + 3] = 0;
                    }
                }

                return pixels;
            }
            finally
            {
                DeleteDC(memory);
            }
        }
        finally
        {
            if (parts.Color != 0)
            {
                DeleteObject(parts.Color);
            }

            if (parts.Mask != 0)
            {
                DeleteObject(parts.Mask);
            }
        }
    }

    private static bool Opaque(Pixels pixels)
    {
        for (var i = 3; i < pixels.Bgra.Length; i += 4)
        {
            if (pixels.Bgra[i] != 255)
            {
                return false;
            }
        }

        return true;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct OverlaidFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        public fixed char DisplayName[260];
        public fixed char TypeName[80];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconParts
    {
        public int IsIcon;
        public uint HotspotX;
        public uint HotspotY;
        public nint Mask;
        public nint Color;
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial int SHParseDisplayName(string name, nint bindContext, nint* pidl, uint attributesIn, uint* attributesOut);

    [LibraryImport("shell32.dll")]
    private static unsafe partial nuint SHGetFileInfoW(char* path, uint attributes, OverlaidFileInfo* info, uint size, uint flags);

    [LibraryImport("shell32.dll")]
    private static unsafe partial int SHGetImageList(int list, Guid* iid, nint* imageList);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetIconInfo(nint icon, out IconParts parts);

    [LibraryImport("ole32.dll")]
    private static partial void CoTaskMemFree(nint memory);
}
