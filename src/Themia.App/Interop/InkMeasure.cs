using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Sdk.Kit;

namespace Themia.App.Interop;

/// <summary>
/// [S-14200] The ink meter behind <see cref="InkMeter"/>: a text laid out by DirectWrite as XAML lays it out (its font,
/// size and weight; a family text's lines at their block line height, the baseline at the font's ascent's share of its
/// own line; a glyph in its font's own line, its em box; wrapped at the width XAML wraps it at), and the room over its
/// first ink and under its last read off the layout's overhang (the outlines' black boxes, fallback fonts included).
/// The shared factory lives as long as the process; every layout and format is released on every path. Measures are
/// kept by text, so a face laid out again costs nothing. Thread-safe (DirectWrite's shared factory is). Never throws:
/// what it cannot measure is null (logged once), and the family's model stands.
/// </summary>
internal static unsafe partial class InkMeasure
{
    private const string Scope = "ink";

    // DirectWrite's v1 interfaces (dwrite.h): the slots this uses.
    private const int QueryInterface = 0;
    private const int Release = 2;
    private const int FactoryCreateTextFormat = 15;
    private const int FactoryCreateTextLayout = 18;
    private const int FormatSetWordWrapping = 5;
    private const int FormatSetLineSpacing = 10;
    private const int LayoutSetMaxHeight = 29;
    private const int LayoutGetLineMetrics = 59;
    private const int LayoutGetMetrics = 60;
    private const int LayoutGetOverhangMetrics = 61;
    private const int Layout1SetCharacterSpacing = 69;

    /// <summary>The most lines a measured text has: a face's few, never a page.</summary>
    private const uint MaxLines = 64;

    private const int SharedFactory = 0;
    private const int NormalStyle = 0;
    private const int NormalStretch = 5;
    private const int UniformSpacing = 1;
    private const int Wrap = 0;
    private const int NoWrap = 1;

    /// <summary>The widest a line is laid out when it does not wrap: no text of a widget's is wider.</summary>
    private const float Unbounded = 100_000f;

    /// <summary>The most measures kept before the memory starts afresh (a seconds clock asks one a second).</summary>
    private const int Kept = 512;

    private static readonly Guid FactoryIid = new(0xb859ee5a, 0xd838, 0x4b5b, 0xa2, 0xe8, 0x1a, 0xdc, 0x7d, 0x93, 0xdb, 0x48);
    private static readonly Guid Layout1Iid = new(0x9064d822, 0x80a7, 0x465c, 0xa9, 0x86, 0xdf, 0x65, 0xf7, 0x8b, 0x8f, 0xeb);
    private static readonly ConcurrentDictionary<InkText, LineInk?> Measured = new();
    private static readonly Lazy<nint> Factory = new(CreateFactory, LazyThreadSafetyMode.ExecutionAndPublication);
    private static int _warned;

    /// <summary>The room in <paramref name="text"/>'s box over its first ink and under its last; null where nothing is inked or DirectWrite will not say.</summary>
    public static LineInk? Measure(InkText text)
    {
        if (Measured.TryGetValue(text, out var known))
        {
            return known;
        }

        var ink = Lay(text);
        if (Measured.Count >= Kept)
        {
            Measured.Clear();
        }

        Measured[text] = ink;
        return ink;
    }

    private static LineInk? Lay(InkText text)
    {
        var factory = Factory.Value;
        if (factory == 0 || string.IsNullOrEmpty(text.Text) || !(text.FontSize > 0) || !double.IsFinite(text.FontSize))
        {
            return null;
        }

        nint format = 0;
        nint layout = 0;
        try
        {
            // A family list ("Segoe Fluent Icons,Segoe MDL2 Assets") draws in its first; DirectWrite's fallback takes a font it lacks.
            var font = (text.Font ?? string.Empty).Split(',')[0].Trim();
            if (font.Length == 0)
            {
                font = "Segoe UI";
            }

            var weight = Math.Clamp(text.Weight, 1, 999);
            int result;
            fixed (char* family = font)
            fixed (char* locale = "en-us")
            {
                result = ((delegate* unmanaged[Stdcall]<nint, char*, nint, int, int, int, float, char*, nint*, int>)Slot(factory, FactoryCreateTextFormat))(
                    factory, family, 0, weight, NormalStyle, NormalStretch, (float)text.FontSize, locale, &format);
            }

            if (result < 0 || format == 0)
            {
                return Failed($"no text format for {font} (0x{result:X8})");
            }

            var wraps = double.IsFinite(text.Width) && text.Width > 0;
            result = ((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(format, FormatSetWordWrapping))(format, wraps ? Wrap : NoWrap);
            if (result < 0)
            {
                return Failed($"no word wrapping (0x{result:X8})");
            }

            fixed (char* chars = text.Text)
            {
                result = ((delegate* unmanaged[Stdcall]<nint, char*, uint, nint, float, float, nint*, int>)Slot(factory, FactoryCreateTextLayout))(
                    factory, chars, (uint)text.Text.Length, format, wraps ? (float)text.Width : Unbounded, Unbounded, &layout);
            }

            if (result < 0 || layout == 0)
            {
                return Failed($"no text layout (0x{result:X8})");
            }

            // Tracking moves where a text wraps: the same spacing XAML puts after each character.
            if (wraps && text.Tracking != 0 && !Track(layout, text))
            {
                return Failed("no character spacing");
            }

            // A family text's lines at their block line height, the baseline where XAML puts it: at the share of the line
            // the font's own line gives its ascent (measured on the renders: 0.812 of a Hero line, not the model's 0.8).
            if (text.LineHeight > 0 && double.IsFinite(text.LineHeight))
            {
                TextMetrics laid;
                result = ((delegate* unmanaged[Stdcall]<nint, TextMetrics*, int>)Slot(layout, LayoutGetMetrics))(layout, &laid);
                if (result < 0 || laid.LineCount == 0 || laid.LineCount > MaxLines)
                {
                    return result < 0 ? Failed($"no text metrics (0x{result:X8})") : null;
                }

                // Every line's metrics or none: DirectWrite fills nothing into a buffer short of the lines.
                var lines = stackalloc LineMetrics[(int)laid.LineCount];
                uint count;
                result = ((delegate* unmanaged[Stdcall]<nint, LineMetrics*, uint, uint*, int>)Slot(layout, LayoutGetLineMetrics))(layout, lines, laid.LineCount, &count);
                if (result < 0 || !(lines[0].Height > 0))
                {
                    return Failed($"no line metrics (0x{result:X8})");
                }

                var baseline = text.LineHeight * lines[0].Baseline / lines[0].Height;
                result = ((delegate* unmanaged[Stdcall]<nint, int, float, float, int>)Slot(layout, FormatSetLineSpacing))(
                    layout, UniformSpacing, (float)text.LineHeight, (float)baseline);
                if (result < 0)
                {
                    return Failed($"no line spacing (0x{result:X8})");
                }
            }

            TextMetrics metrics;
            result = ((delegate* unmanaged[Stdcall]<nint, TextMetrics*, int>)Slot(layout, LayoutGetMetrics))(layout, &metrics);
            if (result < 0 || !(metrics.Height > 0))
            {
                return result < 0 ? Failed($"no text metrics (0x{result:X8})") : null;
            }

            // The box is the lines' own height: the overhang under it is the room under the last ink, as over it the first's.
            result = ((delegate* unmanaged[Stdcall]<nint, float, int>)Slot(layout, LayoutSetMaxHeight))(layout, metrics.Height);
            if (result < 0)
            {
                return Failed($"no layout height (0x{result:X8})");
            }

            Overhang overhang;
            result = ((delegate* unmanaged[Stdcall]<nint, Overhang*, int>)Slot(layout, LayoutGetOverhangMetrics))(layout, &overhang);
            if (result < 0)
            {
                return Failed($"no overhang (0x{result:X8})");
            }

            double over = -overhang.Top, under = -overhang.Bottom;

            // Nothing inked (spaces alone): no ink to stand on.
            return double.IsFinite(over) && double.IsFinite(under) && over + under < metrics.Height ? new LineInk(over, under) : null;
        }
        catch (Exception ex)
        {
            return Failed("measuring a text's ink failed", ex);
        }
        finally
        {
            Free(layout);
            Free(format);
        }
    }

    /// <summary>XAML's <c>CharacterSpacing</c> (thousandths of an em after each character) on the layout (<c>IDWriteTextLayout1</c>).</summary>
    private static bool Track(nint layout, InkText text)
    {
        nint spaced = 0;
        try
        {
            var iid = Layout1Iid;
            var result = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(layout, QueryInterface))(layout, &iid, &spaced);
            if (result < 0 || spaced == 0)
            {
                return false;
            }

            var trailing = (float)(text.FontSize * text.Tracking / 1000.0);
            result = ((delegate* unmanaged[Stdcall]<nint, float, float, float, TextRange, int>)Slot(spaced, Layout1SetCharacterSpacing))(
                spaced, 0f, trailing, 0f, new TextRange { Start = 0, Length = (uint)text.Text.Length });
            return result >= 0;
        }
        finally
        {
            Free(spaced);
        }
    }

    private static nint CreateFactory()
    {
        try
        {
            nint factory;
            var iid = FactoryIid;
            var result = DWriteCreateFactory(SharedFactory, &iid, &factory);
            if (result >= 0 && factory != 0)
            {
                // ignore: the shared factory is the process's for its whole life and is never released.
                return factory;
            }

            Log.Warn(Scope, $"no DirectWrite factory (0x{result:X8}); faces stand on the family's model of their ink");
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "no DirectWrite factory; faces stand on the family's model of their ink", ex);
        }

        return 0;
    }

    private static LineInk? Failed(string why, Exception? ex = null)
    {
        // Once: a face measures on every layout, and the model stands for it either way.
        if (Interlocked.Exchange(ref _warned, 1) == 0)
        {
            Log.Warn(Scope, why + "; faces stand on the family's model of their ink", ex);
        }

        return null;
    }

    private static nint Slot(nint com, int index) => (*(nint**)com)[index];

    private static void Free(nint com)
    {
        if (com != 0)
        {
            ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(com, Release))(com);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TextMetrics
    {
        public float Left;
        public float Top;
        public float Width;
        public float WidthIncludingTrailingWhitespace;
        public float Height;
        public float LayoutWidth;
        public float LayoutHeight;
        public uint MaxBidiReorderingDepth;
        public uint LineCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LineMetrics
    {
        public uint Length;
        public uint TrailingWhitespaceLength;
        public uint NewlineLength;
        public float Height;
        public float Baseline;
        public int IsTrimmed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TextRange
    {
        public uint Start;
        public uint Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Overhang
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;
    }

    [LibraryImport("dwrite.dll")]
    private static partial int DWriteCreateFactory(int factoryType, Guid* iid, nint* factory);
}
