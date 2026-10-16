using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>[S-6602] One rendition an HLS master playlist offers: its peak bitrate and picture size (0 when not declared).</summary>
public readonly record struct HlsVariant(long Bandwidth, int Width, int Height);

/// <summary>
/// [S-6602] The renditions of an HLS master playlist, and the one a tile needs. Windows' adaptive
/// source climbs to the best rendition the network carries, whatever the size it is drawn at: a
/// 1080p stream in a 1000 px tile cost about 290 MB more than its 240p rendition in the switch check,
/// in segment buffers and decoded frames. So the player is capped at the smallest rendition that
/// still fills the tile at its physical size.
/// </summary>
public static class HlsVariants
{
    /// <summary>A master playlist is a few kilobytes; one past this is not read for its renditions.</summary>
    public const int MaxPlaylistChars = 256 * 1024;

    private const string StreamInf = "#EXT-X-STREAM-INF:";

    /// <summary>
    /// The renditions declared by <c>#EXT-X-STREAM-INF</c> lines, in playlist order. Untrusted text:
    /// a line without a positive <c>BANDWIDTH</c> is skipped, a malformed <c>RESOLUTION</c> reads as
    /// not declared, and anything that is not a master playlist (a media playlist, a page) is empty.
    /// </summary>
    public static IReadOnlyList<HlsVariant> Parse(string? playlist)
    {
        if (string.IsNullOrEmpty(playlist) || playlist.Length > MaxPlaylistChars
            || !playlist.TrimStart('﻿', ' ', '\t', '\r', '\n').StartsWith("#EXTM3U", StringComparison.Ordinal))
        {
            return [];
        }

        var variants = new List<HlsVariant>();

        foreach (var raw in playlist.Split('\n'))
        {
            var line = raw.Trim();

            if (!line.StartsWith(StreamInf, StringComparison.Ordinal))
            {
                continue;
            }

            long bandwidth = 0;
            int width = 0, height = 0;

            foreach (var (name, value) in Attributes(line[StreamInf.Length..]))
            {
                if (name == "BANDWIDTH" && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var bits))
                {
                    bandwidth = bits;
                }
                else if (name == "RESOLUTION")
                {
                    (width, height) = Resolution(value);
                }
            }

            if (bandwidth > 0)
            {
                variants.Add(new HlsVariant(bandwidth, width, height));
            }
        }

        return variants;
    }

    /// <summary>
    /// The bitrate to cap the player at for a tile <paramref name="width"/> x <paramref name="height"/>
    /// physical pixels: the smallest rendition that fills it without being stretched up. With
    /// <paramref name="cover"/> (the picture fills the tile and is cropped, or stretched) it must
    /// reach both sides; otherwise (letterboxed) one. Null, no cap, when no rendition is that large,
    /// when any rendition does not declare its size (it cannot be judged), or when nothing is known.
    /// </summary>
    public static long? CapFor(IReadOnlyList<HlsVariant> variants, int width, int height, bool cover)
    {
        ArgumentNullException.ThrowIfNull(variants);

        if (variants.Count == 0 || width <= 0 || height <= 0 || variants.Any(v => v.Width <= 0 || v.Height <= 0))
        {
            return null;
        }

        HlsVariant? best = null;

        foreach (var v in variants)
        {
            var fills = cover ? v.Width >= width && v.Height >= height : v.Width >= width || v.Height >= height;

            if (fills && (best is not { } b || (long)v.Width * v.Height < (long)b.Width * b.Height
                || ((long)v.Width * v.Height == (long)b.Width * b.Height && v.Bandwidth < b.Bandwidth)))
            {
                best = v;
            }
        }

        if (best is not { } chosen)
        {
            return null;
        }

        // Every rendition of that size or smaller may play (the player still steps down on a slow
        // network); the cap is the highest bitrate among them.
        return variants
            .Where(v => (long)v.Width * v.Height <= (long)chosen.Width * chosen.Height)
            .Max(v => v.Bandwidth);
    }

    private static (int Width, int Height) Resolution(string value)
    {
        var x = value.IndexOf('x', StringComparison.OrdinalIgnoreCase);

        return x > 0
            && int.TryParse(value.AsSpan(0, x), NumberStyles.None, CultureInfo.InvariantCulture, out var w)
            && int.TryParse(value.AsSpan(x + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            && w is > 0 and <= 32768 && h is > 0 and <= 32768
            ? (w, h)
            : (0, 0);
    }

    /// <summary>The <c>NAME=value</c> pairs of an attribute list; a quoted value may hold commas.</summary>
    private static IEnumerable<(string Name, string Value)> Attributes(string list)
    {
        var i = 0;

        while (i < list.Length)
        {
            var eq = list.IndexOf('=', i);

            if (eq < 0)
            {
                yield break;
            }

            var name = list[i..eq].Trim();
            var start = eq + 1;
            string value;

            if (start < list.Length && list[start] == '"')
            {
                var close = list.IndexOf('"', start + 1);
                close = close < 0 ? list.Length : close;
                value = list[(start + 1)..close];
                var comma = list.IndexOf(',', Math.Min(close + 1, list.Length));
                i = comma < 0 ? list.Length : comma + 1;
            }
            else
            {
                var comma = list.IndexOf(',', start);
                var end = comma < 0 ? list.Length : comma;
                value = list[start..end].Trim();
                i = end + 1;
            }

            yield return (name, value);
        }
    }
}
