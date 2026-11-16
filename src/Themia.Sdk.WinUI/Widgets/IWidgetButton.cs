namespace Themia.App.Widgets;

/// <summary>
/// [S-2007] A view that is one button from edge to edge (a quick setting's tile, a power button): it
/// takes the press itself, so its card never sees one unhandled. A press on it that moves past the drag
/// threshold is still the card's while the desktop is locked: it unlocks and moves the widget, and the
/// button, losing the pointer to the card, is not pressed. A marker.
/// </summary>
public interface IWidgetButton;
