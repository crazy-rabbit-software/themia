using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>What a key asks a kit list to do.</summary>
public enum KitKeyCommand
{
    /// <summary>Nothing: the key goes on to the surface.</summary>
    None,

    /// <summary>Move the cursor to <see cref="KitKeyResult.Index"/>.</summary>
    Focus,

    /// <summary>Run the focused item's primary action (Enter).</summary>
    Open,

    /// <summary>Flip the focused item's check (Space).</summary>
    Toggle,

    /// <summary>Rename the focused item (F2).</summary>
    Rename,

    /// <summary>Delete the focused item (Delete).</summary>
    Delete,

    /// <summary>The focused item's menu (the Menu key, Shift+F10), at the row.</summary>
    Menu,

    /// <summary>The focused item's first copy (Ctrl+C).</summary>
    Copy,

    /// <summary>Drop the cursor and give the keyboard back (Escape, [S-813]).</summary>
    Release,

    /// <summary>Move the focused item one place up (Alt+Shift+Up, [S-3802]); the cursor goes with it.</summary>
    MoveUp,

    /// <summary>Move the focused item one place down (Alt+Shift+Down).</summary>
    MoveDown,
}

/// <summary>A key's decision: the command and the item it is about (negative for none).</summary>
public readonly record struct KitKeyResult(KitKeyCommand Command, int Index);

/// <summary>
/// [S-7020] The one key map of a kit list, built on <see cref="ListKeys"/> (which names the
/// commands) and reading the list in its reading order, so Left and Right step like Up and Down
/// across columns as <see cref="ItemCursor"/> does. With no item focused yet a movement key lands on
/// an end: End on the last item, every other one on the first ([S-812], as <see cref="ListKeys"/>).
/// Page Up and Page Down jump <see cref="ListKeys.PageStep"/> items. Enter runs the primary action,
/// Space a check, Ctrl+C the first copy, F2 Rename, Delete Delete, the Menu key the menu,
/// Alt+Shift+Up / Down move the focused item, Escape lets go. Pure, so the ends
/// of the list are tested here and not in a view.
/// </summary>
public static class KitKeys
{
    /// <param name="key">The key pressed.</param>
    /// <param name="index">The focused item, or negative for none.</param>
    /// <param name="count">How many items the list holds.</param>
    public static KitKeyResult Decide(WidgetKey key, int index, int count)
    {
        // Left and Right are the reading order's back and forward.
        var mapped = key switch
        {
            WidgetKey.Left => WidgetKey.Up,
            WidgetKey.Right => WidgetKey.Down,
            _ => key,
        };

        // [S-3802] A list that keeps its own order moves the focused item; the ends have nowhere further to go.
        if (mapped is WidgetKey.MoveUp or WidgetKey.MoveDown)
        {
            var up = mapped == WidgetKey.MoveUp;
            return index >= 0 && index < count && (up ? index > 0 : index < count - 1)
                ? new KitKeyResult(up ? KitKeyCommand.MoveUp : KitKeyCommand.MoveDown, index)
                : new KitKeyResult(KitKeyCommand.None, index);
        }

        if (mapped is WidgetKey.Space or WidgetKey.Copy)
        {
            var command = mapped == WidgetKey.Space ? KitKeyCommand.Toggle : KitKeyCommand.Copy;
            return count > 0 && index >= 0 ? new KitKeyResult(command, Math.Min(index, count - 1)) : new KitKeyResult(KitKeyCommand.None, -1);
        }

        var decision = ListKeys.Decide(mapped, index, count);
        return decision.Command switch
        {
            ListCommand.Focus => new KitKeyResult(KitKeyCommand.Focus, decision.Index),
            ListCommand.Open => new KitKeyResult(KitKeyCommand.Open, decision.Index),
            ListCommand.Delete => new KitKeyResult(KitKeyCommand.Delete, decision.Index),
            ListCommand.Rename => new KitKeyResult(KitKeyCommand.Rename, decision.Index),
            ListCommand.Menu => new KitKeyResult(KitKeyCommand.Menu, decision.Index),
            ListCommand.Release => new KitKeyResult(KitKeyCommand.Release, -1),
            _ => new KitKeyResult(KitKeyCommand.None, decision.Index),
        };
    }
}
