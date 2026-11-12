#if DEBUG
namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug renders only: a view that can show a state a render cannot click or hover its way into
/// (an armed button, a hovered row). Optional; <c>--widget-png --state</c> uses it.
/// </summary>
public interface IWidgetPreviewState
{
    /// <summary>The state names this view understands.</summary>
    IReadOnlyList<string> PreviewStates { get; }

    /// <summary>Show <paramref name="state"/>; an unknown name changes nothing.</summary>
    Task ShowPreviewStateAsync(string state);

    /// <summary>
    /// [S-11603] The config the shown state draws when it swapped in one of its own whose size differs
    /// (a shortcuts state holding seven items), so a render sizes the card by what it draws; null when the
    /// state keeps the card's config (its counts, its style) and only fills in sample data.
    /// </summary>
    Themia.Core.Model.WidgetConfig? DrawnConfig => null;
}

#endif
