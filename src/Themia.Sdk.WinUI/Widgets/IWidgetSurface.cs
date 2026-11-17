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
    /// [S-873] Paint nothing, or paint again: the card drops its fill, border and content, so the
    /// wallpaper shows where it stood. For a widget that has nothing
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
