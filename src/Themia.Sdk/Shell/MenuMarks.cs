using Themia.App.Shell;

namespace Themia.Core.Shell;

/// <summary>
/// [S-12952] A menu has one leading column, as Windows 11's own desktop menu (owner, 2026-11-17: "there is one column
/// too much on the left side in the context menu"): a line shows its icon there, and a toggle or one of a set shows
/// its mark in that same slot, never in a check column of its own beside the icons. So a toggle line is a plain line
/// whose icon is the check while on and nothing while off (its own glyph is not drawn, as Explorer's View › Auto
/// arrange icons), and the chosen line of a set shows the dot. Pure.
/// </summary>
public static class MenuMarks
{
    /// <summary>
    /// What a line shows in its one leading slot: for a toggle (<paramref name="checked"/> not null) the check while
    /// on, or the dot for one of a set (<paramref name="choice"/>), and nothing while off; any other line its glyph.
    /// </summary>
    public static string? Slot(bool? @checked, bool choice, string? glyph) => @checked switch
    {
        true => choice ? Glyphs.RadioBullet : Glyphs.Done,
        false => null,
        null => glyph,
    };
}
