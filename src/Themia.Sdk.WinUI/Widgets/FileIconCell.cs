using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6632] The one desktop-icon cell, composed in its item's box (<see cref="Root"/>: a fence's own cell, or the desktop
/// item's whole view): the selection chrome behind it in its palette ([S-6627]), then the cell's stack, its icon slot
/// ([S-6631]) at the desktop's icon size over (or beside) its name, laid by the icon layout (<c>DesktopCellLayout</c>,
/// [S-13970]: the owner adds its name and lays it). The hover is the chrome's (<see cref="Hover"/>); the paint is the
/// owner's, by its own rule (<see cref="Paint"/>).
/// </summary>
public sealed class FileIconCell
{
    /// <param name="root">The item's box: the chrome and the stack go into it, in that order, over what it holds already.</param>
    /// <param name="look">The selection's palette (<c>SelectionLook.DesktopIcon</c>, a fence's over its card).</param>
    /// <param name="side">The icon's side: the desktop's icon size in force.</param>
    /// <param name="placeholder">What the icon slot shows until, or instead of, the picture; null: nothing.</param>
    public FileIconCell(Panel root, SelectionPalette look, double side, FrameworkElement? placeholder = null)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Chrome = SelectionChrome.Wrap(null, look);
        Icon = new IconSlot(side, placeholder) { HorizontalAlignment = HorizontalAlignment.Center };
        Stack.Children.Add(Icon);
        root.Children.Add(Chrome);
        root.Children.Add(Stack);
    }

    /// <summary>The item's box.</summary>
    public Panel Root { get; }

    /// <summary>[S-6627] The selection's mark behind the icon and the name.</summary>
    public Border Chrome { get; }

    /// <summary>The icon and the name, as the icon layout arranges them.</summary>
    public StackPanel Stack { get; } = new();

    /// <summary>[S-6631] The icon: its placeholder under its picture.</summary>
    public IconSlot Icon { get; }

    /// <summary>[S-6627] Hear the pointer come onto the box and leave it (the chrome's own hover rule); disposed with the cell.</summary>
    public IDisposable Hover(Action<bool> hovered) => SelectionChrome.Hover(Root, hovered);

    /// <summary>[S-6627] Mark the cell as <paramref name="cue"/> says, in <paramref name="look"/>.</summary>
    public void Paint(SelectionCue cue, SelectionPalette look) => SelectionChrome.Paint(Chrome, cue, look);
}
