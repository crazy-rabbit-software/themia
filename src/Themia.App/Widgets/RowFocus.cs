using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1288] The keyboard focus of a row in a widget's list (Mail, OneDrive, Notes): the ring that
/// marks it. Which row a key lands on is <see cref="ListKeys"/>'s; the view keeps the index and
/// carries out the command.
/// <para>
/// A click on a row focuses it; Escape lets go. [S-1560] Keys reach the view through the shell's
/// keyboard lease (<see cref="IWidgetKeys"/>), already mapped to <see cref="WidgetKey"/> — there is
/// no per-view <c>KeyDown</c> and no second key vocabulary.
/// </para>
/// </summary>
internal static class RowFocus
{
    /// <summary>The ring's width: inset, like Windows' own list focus, so it never shifts the text.</summary>
    public const double Ring = 1.5;

    /// <summary>Give a row the transparent ring every row carries, so focusing one moves nothing.</summary>
    public static void Prepare(Border row)
    {
        row.BorderThickness = new Thickness(Ring);
        row.BorderBrush = Transparent();

        // [S-814] A row with a focus ring is an item: a press on it is not a press on the background.
        WidgetItems.Mark(row);
    }

    /// <summary>Show or hide the ring on a prepared row.</summary>
    public static void Mark(Border row, bool focused)
    {
        row.BorderBrush = focused ? ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue) : Transparent();

        if (focused)
        {
            row.StartBringIntoView();
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush Transparent() => ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
}
