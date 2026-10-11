using Themia.Core.Widgets;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>What the mixer's rows and the volume tile draw alike: the meter's thickness and the speaker glyph.</summary>
internal static class AudioLook
{
    /// <summary>The meter's thickness: a hairline is lost on a busy wallpaper, more competes with the track.</summary>
    public const double MeterHeight = 2;

    /// <summary>The speaker glyph for an output level: Windows' four, or the crossed-out one when muted.</summary>
    public static string OutputGlyph(double level, bool muted) => VolumeRules.Level(level, muted) switch
    {
        VolumeLevel.Muted => Glyphs.Mute,
        VolumeLevel.Silent => Glyphs.Volume0,
        VolumeLevel.Low => Glyphs.Volume1,
        VolumeLevel.Medium => Glyphs.Volume2,
        _ => Glyphs.Volume3,
    };
}
