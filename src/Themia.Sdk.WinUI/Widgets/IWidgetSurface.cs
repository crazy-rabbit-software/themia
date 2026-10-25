using Microsoft.UI.Xaml;

namespace Themia.App.Widgets;

/// <summary>
/// What a widget view is allowed to ask of the surface it is drawn on.
/// <para>
/// The properties menu is the writer, and the card owns input. A view that needs more — a note,
/// whose text <i>is</i> its config, a list that is typed into — gets it through <see cref="IWidgetSurfaceAware"/> rather than through a static hook or a widened
/// <see cref="IWidgetView.Attach"/>, so nothing that does not need it has to know it exists.
/// </para>
/// </summary>
public interface IWidgetSurface
{
    /// <summary>The user's own title for this widget, or null to use the catalogue label.</summary>
    string? Label { get; }

    /// <summary>
    /// Offer a title the widget knows and the catalogue cannot: the place a Sun &amp; Moon tile is
    /// computed for, the folder a listing is showing.
    /// <para>
    /// Pushed rather than pulled, because it changes when the widget's own state changes and the
    /// card has no way to know when that is. The user's own label still wins: a title they typed
    /// is never overruled by one the widget worked out.
    /// </para>
    /// </summary>
    void SetHeaderTitle(string? title);

    /// <summary>
    /// Persist the config this view has just changed. Must not re-run layout: a relayout would push
    /// the config back into the control the user is typing in.
    /// </summary>
    void RequestSave();

    /// <summary>
    /// Ask for, or give up, the keyboard.
    /// <para>
    /// Outside edit mode the surface carries <c>WS_EX_NOACTIVATE</c>, so it never becomes the
    /// foreground window and no keystroke reaches it — a text box on it would show a caret while the
    /// typing went to whatever app actually had focus. Asking here lifts that for as long as the
    /// widget is being typed into. The cost is inherent to Win32 focus and worth naming: while you
    /// type into a widget, the window you were in loses focus.
    /// </para>
    /// <para>
    /// [S-801] This is the shell's keyboard lease, held for typing: call it from a text box's
    /// GotFocus / LostFocus. A widget that navigates with keys implements <see cref="IWidgetKeys"/>
    /// instead and gets the lease on a click; when its editor closes it keeps the arrows. The lease
    /// ends by itself on Escape, a click elsewhere and the widget going away.
    /// </para>
    /// </summary>
    void RequestTextInput(bool wanted);

    /// <summary>
    /// [S-1251] True when the card is a preview (a debug render) rather than a widget on the
    /// desktop. A view whose buttons act on the machine — the power tile — must be inert there.
    /// </summary>
    bool IsPreview => false;

    /// <summary>
    /// [S-1911] Whether the desktop is in edit mode right now. There a click on a widget selects and
    /// moves it; a view whose whole tile is a button (the power widgets) leaves the press to the card.
    /// </summary>
    bool EditMode => false;

    /// <summary>
    /// [S-3412] The placed widget's id, for state a view keeps across being built again (a view
    /// switch): the gallery's slide. Null where there is no placed widget (a debug render).
    /// </summary>
    string? WidgetId => null;

    /// <summary>
    /// [S-3300] The view links of the view this widget is on: what the widgets there hover and
    /// select, shared. Read in Attach (a card can move to another view between attaches), join with
    /// <see cref="Themia.Core.Links.LinkBoard.Join"/>, dispose the member in Detach. Null where there
    /// is no view (a host without one).
    /// </summary>
    Themia.Core.Links.LinkBoard? Links => null;

    /// <summary>
    /// [S-873] Paint nothing, or paint again: the card drops its fill, border and content and leaves
    /// the window's hit region, so the desktop under it is clickable. For a widget that has nothing
    /// to show right now (music with nothing playing). Edit mode always shows it, so a hidden widget
    /// can still be found, moved and configured. Not persisted: the view decides it from its state.
    /// </summary>
    void SetHidden(bool hidden);
}

/// <summary>Implemented by the few views that edit their own config or take typing.</summary>
public interface IWidgetSurfaceAware
{
    void Bind(IWidgetSurface surface);
}

/// <summary>
/// What a view puts on the right of the header the card draws for it — an unread badge, a count,
/// a busy ring.
/// <para>
/// The header itself belongs to the card, not to the view. Twenty-odd widgets each building their
/// own meant twenty-odd chances to disagree about its size, colour, spacing and behaviour, and it
/// left nowhere to put a setting like "draw it at the bottom instead" without asking every one of
/// them to honour it. The card knows the icon and the title already — the catalogue has one and the
/// instance has the other — so the only thing it cannot know is this.
/// </para>
/// </summary>
public interface IWidgetHeaderContent
{
    /// <summary>The trailing element, or null. Read once when the card builds its header.</summary>
    FrameworkElement? HeaderTrailing { get; }
}
