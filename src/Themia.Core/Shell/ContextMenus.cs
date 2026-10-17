namespace Themia.Core.Shell;

/// <summary>The commands of the desktop surface's own menus: a widget's card, and empty surface in edit mode.</summary>
public enum ShellCommand
{
    /// <summary>A divider.</summary>
    Separator,

    /// <summary>The widget's settings page.</summary>
    Configure,

    /// <summary>Where the widget view's own commands go (refresh, pause); the view supplies them.</summary>
    WidgetActions,

    BringToFront,
    SendToBack,
    Cut,
    Copy,
    Paste,

    /// <summary>Enter edit mode, or leave it ("Done editing").</summary>
    ToggleEditMode,

    Delete,

    /// <summary>[S-1744] The picker, opened where the right-click was.</summary>
    AddWidget,

    /// <summary>[S-1582] Every widget on this display.</summary>
    SelectAll,

    /// <summary>[S-06] The grid snap aid, as on the toolbar.</summary>
    SnapToGrid,

    /// <summary>The app's settings window.</summary>
    Settings,
}

/// <summary>One line of a shell menu: what it does, whether it can be clicked, and a toggle's state.</summary>
public readonly record struct ShellMenuItem(ShellCommand Command, bool Enabled = true, bool? Checked = null)
{
    public bool IsSeparator => Command == ShellCommand.Separator;

    /// <summary>[S-1740] The icon-row command this line is, if any.</summary>
    public QuickCommand? Quick => Command switch
    {
        ShellCommand.Cut => QuickCommand.Cut,
        ShellCommand.Copy => QuickCommand.Copy,
        ShellCommand.Paste => QuickCommand.Paste,
        ShellCommand.Configure or ShellCommand.Settings => QuickCommand.Settings,
        ShellCommand.Delete => QuickCommand.Delete,
        _ => null,
    };

    public MenuLine Line => IsSeparator ? MenuLine.Separator : MenuLine.Entry(Quick);

    internal static ShellMenuItem Divider => new(ShellCommand.Separator);
}

/// <summary>What a widget card's menu depends on, read when it opens.</summary>
/// <param name="EditMode">Whether the desktop is in edit mode.</param>
/// <param name="CanBringToFront">It is not already in front of everything it overlaps.</param>
/// <param name="CanSendToBack">It is not already behind everything it overlaps.</param>
/// <param name="SelectionCount">How many widgets the commands act on ([S-1592]).</param>
/// <param name="CanPaste">Themia's widget clipboard holds something.</param>
/// <param name="HasSettings">[S-1750] The widget's settings page has at least one row.</param>
public readonly record struct CardMenuFacts(
    bool EditMode,
    bool CanBringToFront,
    bool CanSendToBack,
    int SelectionCount,
    bool CanPaste,
    bool HasSettings);

/// <summary>What the empty-surface menu depends on.</summary>
/// <param name="EditMode">Whether the desktop is in edit mode; outside it empty surface is click-through.</param>
/// <param name="Placing">A placement is under way: then a right-click cancels it ([S-1587]) and opens nothing.</param>
/// <param name="CanPaste">Themia's widget clipboard holds something.</param>
/// <param name="HasWidgets">The display shows at least one widget.</param>
/// <param name="GridSnap">The grid snap aid is on.</param>
public readonly record struct SurfaceMenuFacts(bool EditMode, bool Placing, bool CanPaste, bool HasWidgets, bool GridSnap);

/// <summary>[S-1743]..[S-1745] Which commands the shell's menus offer, and in what order.</summary>
public static class ShellMenus
{
    /// <summary>
    /// [S-1743] A widget card's menu in edit mode (outside it, [S-1758]). The icon row is Cut, Copy, Paste, Settings and Delete
    /// ([S-1740]); there is no Duplicate, Copy then Paste does that. The list holds the view's own
    /// commands, the layering pair, and entering or leaving edit mode. Nothing on it changes how the
    /// widget looks: that lives in its settings ([S-1746]). A widget whose page would be empty has no
    /// Settings at all ([S-1750]).
    /// </summary>
    public static IReadOnlyList<ShellMenuItem> ForCard(CardMenuFacts facts)
    {
        if (!facts.EditMode)
        {
            return ForCardInUse(facts);
        }

        var many = facts.SelectionCount > 1;
        var items = new List<ShellMenuItem>
        {
            new(ShellCommand.Cut),
            new(ShellCommand.Copy),
            new(ShellCommand.Paste, Enabled: facts.CanPaste),
        };

        if (facts.HasSettings)
        {
            items.Add(new(ShellCommand.Configure));
        }

        items.Add(new(ShellCommand.Delete));

        items.Add(new(ShellCommand.WidgetActions));
        items.Add(ShellMenuItem.Divider);
        items.Add(new(ShellCommand.BringToFront, Enabled: many || facts.CanBringToFront));
        items.Add(new(ShellCommand.SendToBack, Enabled: many || facts.CanSendToBack));
        items.Add(ShellMenuItem.Divider);
        items.Add(new(ShellCommand.ToggleEditMode));
        return items;
    }

    /// <summary>
    /// [S-1758] A widget card's menu outside edit mode, while the widget is being used rather than
    /// arranged: the view's own commands, its settings ([S-1750]) and the way into edit mode. No Cut,
    /// Copy, Paste, Delete or layering: those arrange the desktop, and outside edit mode there is
    /// nowhere to paste and no selection shown, so they did nothing the user could see.
    /// </summary>
    private static List<ShellMenuItem> ForCardInUse(CardMenuFacts facts)
    {
        var items = new List<ShellMenuItem> { new(ShellCommand.WidgetActions), ShellMenuItem.Divider };

        if (facts.HasSettings)
        {
            items.Add(new(ShellCommand.Configure));
        }

        items.Add(new(ShellCommand.ToggleEditMode));
        return items;
    }

    /// <summary>
    /// [S-1744] The menu of empty surface, in edit mode and not while placing: Paste and Settings in
    /// the icon row; Add widget, Select all, Snap to grid and Done editing in the list. Outside edit
    /// mode, or while placing, there is none.
    /// </summary>
    public static IReadOnlyList<ShellMenuItem> ForSurface(SurfaceMenuFacts facts)
    {
        if (!facts.EditMode || facts.Placing)
        {
            return [];
        }

        return
        [
            new(ShellCommand.Paste, Enabled: facts.CanPaste),
            new(ShellCommand.Settings),
            new(ShellCommand.AddWidget),
            new(ShellCommand.SelectAll, Enabled: facts.HasWidgets),
            ShellMenuItem.Divider,
            new(ShellCommand.SnapToGrid, Checked: facts.GridSnap),
            ShellMenuItem.Divider,
            new(ShellCommand.ToggleEditMode),
        ];
    }
}

/// <summary>What a left-button press on a widget card in edit mode starts.</summary>
public enum CardPress
{
    /// <summary>Nothing: not edit mode, or not the left button.</summary>
    None,

    /// <summary>[S-1580] Ctrl+click: in or out of the selection, and nothing else.</summary>
    ToggleSelect,

    /// <summary>A press on a resize handle.</summary>
    Resize,

    /// <summary>A press on the card itself: a move if it travels, a click if it does not.</summary>
    Move,
}

/// <summary>[S-1745] Telling a click on a card from a drag and from selecting, as pure rules.</summary>
public static class CardGestures
{
    /// <summary>DIPs the pointer must travel, on either axis, before a press is a drag rather than a click.</summary>
    public const double DragThreshold = 3;

    /// <summary>What a press starts.</summary>
    public static CardPress Press(bool editMode, bool leftButton, bool ctrl, bool onHandle)
    {
        if (!editMode || !leftButton)
        {
            return CardPress.None;
        }

        if (ctrl)
        {
            return CardPress.ToggleSelect;
        }

        return onHandle ? CardPress.Resize : CardPress.Move;
    }

    /// <summary>Whether the pointer has travelled far enough from the press to be a drag.</summary>
    public static bool Travelled(double dx, double dy)
        => !double.IsFinite(dx) || !double.IsFinite(dy) || Math.Abs(dx) >= DragThreshold || Math.Abs(dy) >= DragThreshold;

    /// <summary>
    /// [S-1745] Whether the gesture that just ended was a plain click: a press on the card (not a
    /// handle, not Ctrl), released by the user rather than lost to capture, without travelling.
    /// </summary>
    public static bool IsClick(CardPress press, bool travelled, bool released)
        => press == CardPress.Move && !travelled && released;

    /// <summary>
    /// [S-1745] Whether a click shows the widget's page in the settings window: only a plain click,
    /// and only when the window is already open; a click never opens it.
    /// </summary>
    public static bool FollowsInSettings(CardPress press, bool travelled, bool released, bool settingsOpen)
        => settingsOpen && IsClick(press, travelled, released);

    /// <summary>
    /// [S-1751] Whether the widget's own content (header and view) takes pointer input. In edit mode
    /// it does not: a press, right-click or hover is the card's (select, drag, the card menu), never
    /// a row's, a button's or an item menu's.
    /// </summary>
    public static bool ContentTakesInput(bool editMode) => !editMode;
}
