using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-827] The font families installed on this PC, for the Appearance page's font picker — every
/// family, as the original listed (<c>list_system_fonts</c>), not a hand-picked six.
/// <para>
/// GDI's <c>EnumFontFamiliesExW</c> rather than DirectWrite: it is one flat call with a plain
/// callback, needs no COM vtable, and its family names are ones XAML's <c>FontFamily</c> resolves.
/// The list is read off the UI thread and cached, so Settings never re-enumerates on open. [S-6443]
/// A font installed or removed while the app runs (Windows broadcasts <c>WM_FONTCHANGE</c> to the
/// command window) drops the cache and raises <see cref="Changed"/>: an open picker reloads, a
/// later one reads the new list.
/// </para>
/// </summary>
internal static unsafe partial class SystemFonts
{
    private const string Scope = "fonts";

    private const byte DefaultCharset = 1;

    private const byte SymbolCharset = 2;

    private const uint TrueTypeFontType = 4;

    /// <summary>Where <c>NEWTEXTMETRICEXW.ntmFontSig.fsUsb[0]</c> sits: after the 76-byte <c>NEWTEXTMETRICW</c>.</summary>
    private const int UnicodeSubsetsOffset = 76;

    /// <summary><c>fsUsb[0]</c> bit 0: the font covers Basic Latin.</summary>
    private const uint BasicLatin = 1;

    private const int LfFaceSize = 32;

    private static Task<InstalledFonts>? _cache;

    /// <summary>[S-6443] The installed fonts changed; <see cref="LoadAsync"/> now reads them afresh. UI thread.</summary>
    public static event Action? Changed;

    /// <summary>The installed families, sorted and de-duplicated, and which are symbol fonts. Empty (logged) if GDI fails.</summary>
    public static Task<InstalledFonts> LoadAsync() => _cache ??= Task.Run(Enumerate);

    /// <summary>[S-6443] <c>WM_FONTCHANGE</c>: forget the list and tell whoever shows it.</summary>
    public static void FontsChanged()
    {
        _cache = null;
        Log.Info(Scope, "the installed fonts changed");
        Changed?.Invoke();
    }

    private static InstalledFonts Enumerate()
    {
        var names = new List<(string, bool)>();
        var dc = GetDC(0);
        if (dc == 0)
        {
            Log.Warn(Scope, "no screen DC; the font list is only the presets");
            return InstalledFonts.None;
        }

        var handle = GCHandle.Alloc(names);
        try
        {
            var logFont = default(LogFontW);
            logFont.Charset = DefaultCharset;
            EnumFontFamiliesExW(dc, &logFont, &OnFamily, GCHandle.ToIntPtr(handle), 0);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "font enumeration failed; the font list is only the presets", ex);
        }
        finally
        {
            handle.Free();
            _ = ReleaseDC(0, dc);
        }

        var fonts = InstalledFonts.From(names);
        Log.Info(Scope, $"{fonts.Families.Count} installed font families ({fonts.Symbol.Count} symbol)");
        return fonts;
    }

    /// <summary>GDI's callback: one call per family and script. Must never let an exception out.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static int OnFamily(LogFontW* font, void* metrics, uint type, nint state)
    {
        try
        {
            if (GCHandle.FromIntPtr(state).Target is List<(string, bool)> names)
            {
                names.Add((new string(font->FaceName, 0, LfFaceSize).TrimEnd('\0').Split('\0')[0], CannotSpell(font, metrics, type)));
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "a font family could not be read", ex);
        }

        return 1; // keep enumerating
    }

    /// <summary>
    /// [S-6440] Whether the face cannot draw its own (Latin) name: a symbol-charset face (Wingdings), or
    /// a TrueType/OpenType face whose Unicode signature lacks Basic Latin (Segoe Fluent Icons, which
    /// keeps its glyphs in the private use area). Raster and vector faces report no signature.
    /// </summary>
    private static bool CannotSpell(LogFontW* font, void* metrics, uint type)
    {
        if (font->Charset == SymbolCharset)
        {
            return true;
        }

        return (type & TrueTypeFontType) != 0 && metrics is not null
            && (*(uint*)((byte*)metrics + UnicodeSubsetsOffset) & BasicLatin) == 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LogFontW
    {
        public int Height;
        public int Width;
        public int Escapement;
        public int Orientation;
        public int Weight;
        public byte Italic;
        public byte Underline;
        public byte StrikeOut;
        public byte Charset;
        public byte OutPrecision;
        public byte ClipPrecision;
        public byte Quality;
        public byte PitchAndFamily;
        public fixed char FaceName[LfFaceSize];
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint dc);

    [LibraryImport("gdi32.dll", EntryPoint = "EnumFontFamiliesExW")]
    private static partial int EnumFontFamiliesExW(
        nint dc,
        LogFontW* logFont,
        delegate* unmanaged[Stdcall]<LogFontW*, void*, uint, nint, int> callback,
        nint state,
        uint flags);
}
