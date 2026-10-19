using Microsoft.UI.Xaml;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6902] A widget module: what the host creates from <c>[assembly: ThemiaWidget(id, typeof(M))]</c>.
/// Its <see cref="Descriptor"/> is the pure half; what it adds is the UI the host asks of it.
/// </summary>
public interface IWidgetModule
{
    WidgetDescriptor Descriptor { get; }

    /// <summary>
    /// Build a new view. It is a <see cref="FrameworkElement"/> that also implements the host's view
    /// contract. <paramref name="host"/> is what the view may ask of the host ([S-6913]); a view that
    /// needs it keeps it.
    /// </summary>
    FrameworkElement CreateView(IWidgetHost host);

    /// <summary>
    /// [S-3719] The widget's own lines on its properties menu for <paramref name="config"/> (always
    /// its own config type, the live one: change it in place, then <see cref="WidgetMenuLines.Save"/>).
    /// None, the default, is a widget with no options of its own: its menu is the card's lines alone.
    /// </summary>
    void MenuLines(WidgetMenuLines menu, Themia.Core.Model.WidgetConfig config)
    {
    }
}
