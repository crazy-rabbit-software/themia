#if DEBUG
namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug renders only: a view that can show a state a render cannot click or hover its way into
/// (an armed button, a hovered row). Optional; <c>--widget-png --state</c> uses it.
/// </summary>
internal interface IWidgetPreviewState
{
    /// <summary>The state names this view understands.</summary>
    IReadOnlyList<string> PreviewStates { get; }

    /// <summary>Show <paramref name="state"/>; an unknown name changes nothing.</summary>
    Task ShowPreviewStateAsync(string state);
}

#endif
