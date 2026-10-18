namespace Themia.Core.Widgets;

/// <summary>[S-4842] What the Recent files list shows at one size.</summary>
/// <param name="Compact">32-DIP rows rather than 40.</param>
/// <param name="TwoLine">The folder under the name (else the name alone).</param>
/// <param name="TimeColumn">When it was opened, at the row's end.</param>
public readonly record struct RecentFilesPlan(bool Compact, bool TwoLine, bool TimeColumn);

/// <summary>
/// [S-4842] The Recent files ladder: the list is the widget. A short tile has compact one-line rows;
/// from a Regular height each row carries the folder under the name; from an Expanded width the
/// time opened stands at the end ("2h"). The rest of the list scrolls.
/// </summary>
public static class RecentFilesLayout
{
    public static RecentFilesPlan Plan(WidgetArea area)
    {
        var twoLine = area.HeightClass >= SizeClass.Regular && area.WidthClass >= SizeClass.Regular;
        var compact = !twoLine && area.HeightClass == SizeClass.Compact;
        return new RecentFilesPlan(compact, twoLine, area.WidthClass >= SizeClass.Expanded);
    }
}
