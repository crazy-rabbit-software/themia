using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// The icon Explorer shows for a file, a folder, an executable or a Windows location
/// (<c>shell:RecycleBinFolder</c>, <c>shell:Downloads</c>), as something XAML can draw.
/// <para>
/// One extraction path for all of them: the shell item for the parsing name, asked through
/// <c>IShellItemImageFactory</c> for its icon (never a thumbnail) at the pixel size wanted. A
/// <c>shell:</c> name has no file-system path, so this is also the only way to get the Recycle Bin's
/// or This PC's real icon ([S-1383]); and asking at the drawn size keeps a 48 DIP tile sharp at 200%.
/// </para>
/// <para>
/// The extraction runs on the thread pool, because the first look at a binary is disk I/O. Only the
/// last step, wrapping the pixels in a bitmap, happens on the UI thread, where XAML objects must be
/// made. Each (path, size, variant) is extracted once per run and the bitmap shared by every widget
/// that shows it; one with no usable icon is remembered as such. A <c>variant</c> keeps two states of
/// one item apart (the Recycle Bin empty and full), so the one cached never stands for the other.
/// </para>
/// <para>
/// Every handle the extraction takes (the COM object, the bitmap, the memory DC) is released on
/// every path out, by the <c>finally</c> that owns it. Callers decide what may be probed at all
/// (<see cref="Themia.Core.Widgets.LaunchTargets.MayProbe"/>): looking at a UNC path connects to its
/// server.
/// </para>
/// </summary>
internal static partial class AppIcons
{
    private const string Scope = "app-icons";

    /// <summary>Pixels per side by default: crisp at 200% for an icon drawn at 20 DIPs, and still small in memory.</summary>
    public const int DefaultSize = 48;

    private static readonly Dictionary<string, ImageSource?> Ready = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Task<Pixels?>> Pending = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// One extraction at a time. The shell answers E_PENDING to concurrent first-time icon requests
    /// (its icon cache is still filling for the first one), so asking one by one is what makes the
    /// first answer the real one.
    /// </summary>
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);

    private sealed record Pixels(int Width, int Height, byte[] Bgra);

    /// <summary>The icon for <paramref name="path"/> at the default size, or null when it has none.</summary>
    public static Task<ImageSource?> GetAsync(string path) => GetAsync(path, DefaultSize);

    /// <summary>
    /// The icon for <paramref name="path"/> (a file-system path or a <c>shell:</c> name) at
    /// <paramref name="size"/> pixels, or null when it has none. UI thread only: it builds the bitmap
    /// there, and the two caches are only ever touched from it.
    /// </summary>
    public static async Task<ImageSource?> GetAsync(string path, int size, string? variant = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        size = Math.Clamp(size, 16, 256);
        var key = string.Concat(size.ToString(System.Globalization.CultureInfo.InvariantCulture), "|", variant, "|", path);

        if (Ready.TryGetValue(key, out var known))
        {
            return known;
        }

        if (!Pending.TryGetValue(key, out var task))
        {
            task = ExtractSerialisedAsync(path, size, IconOnly);
            Pending[key] = task;
        }

        Pixels? pixels;
        try
        {
            pixels = await task.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"could not extract the icon of {path}", ex);
            pixels = null;
        }

        Pending.Remove(key);

        if (Ready.TryGetValue(key, out known))
        {
            return known;
        }

        var image = pixels is null ? null : ToBitmap(pixels);
        Ready[key] = image;
        return image;
    }

    /// <summary>
    /// [S-1324] The icon, or with <paramref name="thumbnail"/> the thumbnail of the item's contents (a
    /// photo's picture), at <paramref name="size"/> pixels — <b>not cached</b>: for the folder widget,
    /// whose items come and go with every folder it shows and which keeps its own bounded cache. Null
    /// when the item has none (a thumbnail of a text file). UI thread, like <see cref="GetAsync(string, int, string?)"/>.
    /// </summary>
    public static async Task<ImageSource?> LoadAsync(string path, int size, bool thumbnail)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var pixels = await ExtractSerialisedAsync(path, Math.Clamp(size, 16, 256), thumbnail ? ThumbnailOnly : IconOnly)
                .ConfigureAwait(true);
            return pixels is null ? null : ToBitmap(pixels);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"could not extract the image of {path}", ex);
            return null;
        }
    }

    private static async Task<Pixels?> ExtractSerialisedAsync(string path, int size, int flags)
    {
        await OneAtATime.WaitAsync().ConfigureAwait(false);

        try
        {
            return await Task.Run(() => Extract(path, size, flags)).ConfigureAwait(false);
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private static WriteableBitmap ToBitmap(Pixels pixels)
    {
        var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
        pixels.Bgra.CopyTo(bitmap.PixelBuffer);
        bitmap.Invalidate();
        return bitmap;
    }

    private static unsafe Pixels? Extract(string path, int size, int flags)
    {
        // The shell's objects want COM on this thread. Multithreaded, so a pool thread needs no
        // message pump; an apartment-threaded icon handler is hosted for us by COM. A thread that is
        // already initialised the other way (RPC_E_CHANGED_MODE) is used as it is and not
        // uninitialised.
        var initialised = CoInitializeEx(0, CoInitMultithreaded);
        nint factory = 0;
        nint bitmap = 0;

        try
        {
            var iid = ImageFactoryIid;

            if (SHCreateItemFromParsingName(ShellNameFor(path), 0, &iid, &factory) < 0 || factory == 0)
            {
                return null;
            }

            // IShellItemImageFactory::GetImage is slot 3, after IUnknown's three.
            var getImage = (delegate* unmanaged[Stdcall]<nint, IconSize, int, nint*, int>)(*(nint**)factory)[3];

            var hrImage = getImage(factory, new IconSize(size, size), flags, &bitmap);

            // E_PENDING: the shell is still extracting this icon for an earlier caller. Asked once
            // more, at once (no wait); a second E_PENDING is an icon this run does without.
            if (hrImage == EPending)
            {
                hrImage = getImage(factory, new IconSize(size, size), flags, &bitmap);
            }

            if (hrImage < 0 || bitmap == 0)
            {
                return null;
            }

            return Read(bitmap);
        }
        finally
        {
            if (bitmap != 0)
            {
                DeleteObject(bitmap);
            }

            if (factory != 0)
            {
                var release = (delegate* unmanaged[Stdcall]<nint, uint>)(*(nint**)factory)[2];
                release(factory);
            }

            if (initialised >= 0)
            {
                CoUninitialize();
            }
        }
    }

    /// <summary>
    /// [S-3960] The shell name whose icon stands for <paramref name="path"/>: a packaged app's
    /// executable is asked for as its Apps-folder item (<c>shell:AppsFolder\&lt;AUMID&gt;</c>, from the
    /// package's manifest), which draws the package's logo; the file itself often has no icon
    /// (WhatsApp.Root.exe) and drew the blank program icon. Everything else is asked for as it is.
    /// Runs inside the extraction, so the manifest is read once per cached (path, size).
    /// </summary>
    private static string ShellNameFor(string path)
        => PackagedApps.PackageOf(path) is null ? path : PackagedApps.AppsFolderTarget(path, ReadManifest) ?? path;

    /// <summary>A package's manifest text; null when there is none (quietly) or it cannot be read (logged).</summary>
    private static string? ReadManifest(string file)
    {
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists)
            {
                return null;
            }

            if (info.Length > PackagedApps.MaxManifestCharacters * 4L)
            {
                Log.Warn(Scope, $"the package manifest {file} is {info.Length} bytes; not read");
                return null;
            }

            return File.ReadAllText(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Warn(Scope, $"could not read the package manifest {file}", ex);
            return null;
        }
    }

    /// <summary>A 32-bit bitmap's pixels as premultiplied BGRA, or null for one that cannot be read.</summary>
    private static unsafe Pixels? Read(nint bitmap)
    {
        BitmapHeader bm;
        if (GetObject(bitmap, sizeof(BitmapHeader), &bm) == 0 || bm.Width <= 0 || bm.Height <= 0 || bm.Width > 256 || bm.Height > 256)
        {
            return null;
        }

        var memory = CreateCompatibleDC(0);
        if (memory == 0)
        {
            return null;
        }

        try
        {
            var width = bm.Width;
            var height = bm.Height;
            var color = new byte[width * height * 4];

            if (!Bits(memory, bitmap, width, height, color))
            {
                return null;
            }

            var hasAlpha = false;
            var straight = false;

            for (var i = 0; i < color.Length; i += 4)
            {
                var alpha = color[i + 3];
                hasAlpha |= alpha != 0;

                // A channel brighter than its alpha cannot be premultiplied: the shell handed over
                // straight alpha (it does for some icons and not others), so it is converted below.
                straight |= color[i] > alpha || color[i + 1] > alpha || color[i + 2] > alpha;
            }

            if (!hasAlpha)
            {
                // No alpha channel at all: an opaque bitmap.
                for (var i = 3; i < color.Length; i += 4)
                {
                    color[i] = 255;
                }

                return new Pixels(width, height, color);
            }

            if (straight)
            {
                // XAML wants premultiplied alpha.
                for (var i = 0; i < color.Length; i += 4)
                {
                    var alpha = color[i + 3];
                    if (alpha == 255)
                    {
                        continue;
                    }

                    color[i] = (byte)(color[i] * alpha / 255);
                    color[i + 1] = (byte)(color[i + 1] * alpha / 255);
                    color[i + 2] = (byte)(color[i + 2] * alpha / 255);
                }
            }

            return new Pixels(width, height, color);
        }
        finally
        {
            DeleteDC(memory);
        }
    }

    private static unsafe bool Bits(nint dc, nint bitmap, int width, int height, byte[] target)
    {
        var header = new BitmapInfoHeader
        {
            Size = (uint)sizeof(BitmapInfoHeader),
            Width = width,

            // Negative: top-down rows, the order the bitmap wants them in.
            Height = -height,
            Planes = 1,
            BitCount = 32,
        };

        fixed (byte* bits = target)
        {
            return GetDIBits(dc, bitmap, 0, (uint)height, bits, &header, 0) == height;
        }
    }

    /// <summary>IShellItemImageFactory's SIIGBF_ICONONLY: the item's icon, never a thumbnail of its contents.</summary>
    private const int IconOnly = 0x4;

    /// <summary>SIIGBF_THUMBNAILONLY: the contents' thumbnail, or failure — never the icon in its place.</summary>
    private const int ThumbnailOnly = 0x8;

    private const int EPending = unchecked((int)0x8000000A);

    private const uint CoInitMultithreaded = 0x0;

    /// <summary>IID_IShellItemImageFactory.</summary>
    private static readonly Guid ImageFactoryIid = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct IconSize(int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapHeader
    {
        public int Type;
        public int Width;
        public int Height;
        public int WidthBytes;
        public ushort Planes;
        public ushort BitsPixel;
        public nint Bits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;

        // GetDIBits writes a colour table after the header for some formats; for 32-bit BI_RGB it
        // writes none, but the documented buffer is a BITMAPINFO, so room for one entry is kept.
        public uint Palette0;
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial int SHCreateItemFromParsingName(string path, nint bindContext, Guid* iid, nint* item);

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint flags);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static unsafe partial int GetObject(nint handle, int size, void* buffer);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint handle);

    [LibraryImport("gdi32.dll")]
    private static unsafe partial int GetDIBits(nint dc, nint bitmap, uint start, uint lines, void* bits, BitmapInfoHeader* info, uint usage);
}
