using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-800] A view whose widget takes the keyboard when it is clicked: a list navigated with the
/// arrows, a calendar paged with Left / Right, a slideshow stepped and paused.
/// <para>
/// Implementing this is the whole of opting in. The card asks the shell's keyboard lease for the
/// keyboard on a click anywhere in the widget (outside edit mode), the shell maps the platform keys
/// to <see cref="WidgetKey"/> and hands them here while the widget holds it, and gives the keyboard
/// back on Escape, on a click on anything else (another widget, the desktop, another app) and when
/// the widget goes away. A view that is only typed into does not implement this; its text box asks
/// through <see cref="IWidgetSurface.RequestTextInput"/>, which rides the same lease.
/// </para>
/// </summary>
public interface IWidgetKeys
{
    /// <summary>
    /// Act on a key; return whether it was used (an unused key goes on to the surface, so Tab can
    /// still move focus). Keys never arrive while a text box has focus: the box owns them.
    /// <para>
    /// <see cref="WidgetKey.Escape"/> also arrives whenever the user is done with the widget some other
    /// way ([S-813]..[S-815]) — a press on its background, on another widget, outside Themia (the
    /// desktop, the taskbar, another app), another app taking the foreground, edit mode — so dropping
    /// the cursor or selection on Escape is the one place a view clears it. It may arrive after <see cref="IWidgetView.Detach"/>
    /// and with nothing selected; both must be harmless.
    /// </para>
    /// </summary>
    bool HandleKey(WidgetKey key);

    /// <summary>
    /// [S-1564] Whether a click takes the keyboard right now. A view whose keys belong to a part it
    /// shows only at some sizes (a meter's process list) says no while that part is off screen, so a
    /// click on a small tile never takes the keyboard from the foreground app for nothing.
    /// </summary>
    bool WantsKeys => true;

    /// <summary>
    /// [S-6626] Whether the view keeps its item selection when the user goes on to another widget or
    /// window, drawn inactive, as the desktop keeps its icons selected (the item-selecting widgets,
    /// [S-6620]). Such a view is sent <see cref="WidgetKey.Deactivate"/> instead of
    /// <see cref="WidgetKey.Escape"/> then; Escape itself, a press on its own empty space and edit
    /// mode still clear it.
    /// </summary>
    bool KeepsSelectionWhenLeft => false;

    /// <summary>
    /// [S-1980]/[S-1997] A character typed while the widget holds the lease and no text box has focus:
    /// type-ahead in a long list (<see cref="TypeAhead"/>). Return whether it was used. Arrives after
    /// the key's own <see cref="HandleKey"/>, so a letter that is a widget key (M) reaches both; a
    /// view without type-ahead leaves this alone.
    /// </summary>
    bool HandleText(char character) => false;
}
