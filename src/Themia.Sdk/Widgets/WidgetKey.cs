namespace Themia.Core.Widgets;

/// <summary>
/// [S-803] The keys a widget may be handed while it holds the keyboard lease: the navigation set the
/// classic widgets answered through their low-level keyboard hook (arrows, Page Up / Down, Home /
/// End, Tab, Enter, Space, Delete, F2, the Menu key, Escape).
/// <para>
/// A closed set rather than a raw virtual-key code, so a widget's key map is a pure function that
/// can be tested here, and the shell's keyboard lease (which decides <i>whether</i> a widget gets a
/// key) is the only place that knows about platform key codes (<see cref="WidgetKeys"/>).
/// </para>
/// </summary>
public enum WidgetKey
{
    Left,
    Right,
    Up,
    Down,
    Home,
    End,
    Enter,

    /// <summary>
    /// Let go. Also what the shell sends when the lease ends any other way (a click elsewhere, the
    /// surface losing activation, edit mode), so a view has one place that drops its cursor.
    /// </summary>
    Escape,

    PageUp,
    PageDown,
    Tab,
    ShiftTab,
    Space,
    Delete,

    /// <summary>F2.</summary>
    Rename,

    /// <summary>The Menu key or Shift+F10: the focused item's context menu.</summary>
    Menu,

    /// <summary>[S-1225] + (main row or numeric pad): turn a level up.</summary>
    Plus,

    /// <summary>[S-1225] − (main row or numeric pad): turn a level down.</summary>
    Minus,

    /// <summary>
    /// [S-1225] The M key: mute in the audio widgets. The one letter that is a widget key — every
    /// other letter stays type-ahead, and a view that has no mute simply does not use it.
    /// </summary>
    Mute,

    /// <summary>[S-1468] Shift+Left: a step back within the item (now playing seeks back five seconds).</summary>
    ShiftLeft,

    /// <summary>[S-1468] Shift+Right: a step forward within the item.</summary>
    ShiftRight,

    /// <summary>[S-1327] Ctrl+C: copy the selected items (the folder widget's files).</summary>
    Copy,

    /// <summary>[S-1327] Ctrl+X: cut the selected items.</summary>
    Cut,

    /// <summary>[S-1327] Ctrl+V: paste into the widget.</summary>
    Paste,

    /// <summary>[S-1327] Ctrl+A: select every item.</summary>
    SelectAll,

    /// <summary>[S-6417] Ctrl+Shift+C: copy the selected items' paths, as Explorer's Copy as path.</summary>
    CopyAsPath,

    /// <summary>[S-6417] Alt+Enter: the selected items' properties, as Explorer's.</summary>
    Properties,

    /// <summary>[S-1327] Backspace: back through the widget's own history.</summary>
    Back,

    /// <summary>[S-3802] Alt+Shift+Up: move the focused item one place up (the drag's keyboard twin).</summary>
    MoveUp,

    /// <summary>[S-3802] Alt+Shift+Down: move the focused item one place down.</summary>
    MoveDown,
}
