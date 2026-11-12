using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-11170] A fact atom's pick on its properties menu ([S-3729], as the e-mail table's columns): a
/// submenu listing the picked facts in their order, each dragged up or down to reorder and removed from
/// its own line (greyed on the last one: one row always stays), then an Add line for each fact not
/// picked. Every change is made in <paramref name="picked"/> and saved; the card's height follows the
/// count ([S-2447]).
/// </summary>
public static class SkyFactLines
{
    public static ItemMenuEntry Pick<T>(WidgetMenuLines menu, string? glyph, string text, List<T> picked, IReadOnlyList<T> all, Func<T, string> name, Func<T, string?> factGlyph)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(picked);
        ArgumentNullException.ThrowIfNull(all);

        var add = new List<ItemMenuEntry>();
        foreach (var fact in all.Where(f => !picked.Contains(f)))
        {
            add.Add(menu.Command(Glyphs.Add, name(fact), () => picked.Add(fact)));
        }

        return menu.List(glyph, text, picked, new MenuList<T>(name, string.Empty)
        {
            Glyph = factGlyph,
            AddLines = add,
            Min = 1,
        });
    }
}
