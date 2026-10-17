using Microsoft.UI.Xaml;
using Themia.Core.Model;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6583] A view whose content can sit at the start, the middle or the end of its card
/// (<see cref="Themia.Core.Widgets.ContentAlignment"/>). The card tells it where, before it attaches
/// and on every change, as <see cref="Themia.Core.Widgets.ContentAlignment.Shown"/> resolves it: the
/// widget's own choice, or the kind's default for its style. A view lays itself out again when the
/// value changes; the size ladder ([S-660]) is untouched, only where the block it built sits.
/// <para>
/// Optional, and only for the kinds the Core rule lists: <c>ContentAlignmentTests</c> checks the
/// two agree.
/// </para>
/// </summary>
public interface IWidgetAligned
{
    void ApplyAlign(ContentAlign align);
}

/// <summary>[S-6583] An alignment as WinUI's: the element's place in its slot and its text's.</summary>
public static class Aligned
{
    /// <summary>Where an element sits across its slot: start, middle or end (flow-relative, as Windows aligns).</summary>
    public static HorizontalAlignment Horizontal(ContentAlign align) => align switch
    {
        ContentAlign.Center => HorizontalAlignment.Center,
        ContentAlign.Right => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Left,
    };

    /// <summary>How its lines of text sit: start, middle or end.</summary>
    public static TextAlignment Text(ContentAlign align) => align switch
    {
        ContentAlign.Center => TextAlignment.Center,
        ContentAlign.Right => TextAlignment.Right,
        _ => TextAlignment.Left,
    };
}
