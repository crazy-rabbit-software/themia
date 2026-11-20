using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Themia.Core.Files;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-5549] [S-5564] A view drawn as a desktop icon: what a drag of the desktop's selection shows of it.
/// The host composes the held icon's picture into the move's picture ([S-13048]: it alone, however many move).
/// [S-11878] On the desktop an icon moves as every widget does (its card's move, [S-2008]); the card hands
/// the press to <see cref="DragOut"/> only once the move is to leave the desktop or reach a drop target.
/// </summary>
public interface IDesktopIconView
{
    /// <summary>The icon's own square (never its name or its highlight): where the pointer grabbed it is measured from its top left.</summary>
    FrameworkElement DragIconBox { get; }

    /// <summary>
    /// The icon's picture as it is drawn now, kept current by the view (read from the bitmap it shows, or
    /// rendered ahead whenever the icon changes), so a drag's ghost is composed without rendering
    /// anything. Null while the view has none yet.
    /// </summary>
    GhostIcon? DragIcon { get; }

    /// <summary>
    /// [S-11878] Whether files dropped on this icon go somewhere (a folder, a program, the Recycle Bin, an
    /// own icon still waiting for its target): an icon moved over it then becomes the shell's drag, so the drop
    /// reaches it.
    /// </summary>
    bool TakesDrops { get; }

    /// <summary>
    /// [S-11878] The card's move of the selection becomes the shell's drag of it, from this icon, with the
    /// press still down at <paramref name="point"/> (the current pointer event's point): called inside that
    /// pointer event, the card having let the pointer go. The view starts it as it would its own drag
    /// ([S-5549]: <see cref="ItemDrag.StartAsync"/>).
    /// </summary>
    void DragOut(PointerPoint point);
}
