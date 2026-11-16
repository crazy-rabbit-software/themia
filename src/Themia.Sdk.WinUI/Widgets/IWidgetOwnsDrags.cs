namespace Themia.App.Widgets;

/// <summary>
/// [S-2008] A view in which a press that moves is the view's own gesture: it drags what it shows (a
/// folder's files, ink on the scratchpad, a habit across days). Dragging such a widget never moves its
/// card on the locked desktop; every other widget, a desktop icon included ([S-11878]), pressed and
/// dragged while the desktop is locked, moves. A marker: a view without it leaves a moving press to its card.
/// </summary>
public interface IWidgetOwnsDrags;
