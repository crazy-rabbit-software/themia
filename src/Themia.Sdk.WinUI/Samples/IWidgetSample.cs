namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] A view that can show itself on the sample day's data (<c>Themia.Sdk.Samples</c>) instead of
/// its live sources: the add page's picture of it ([S-11710]), in every build. It lives in the module's
/// <c>Samples/</c> folder, which ships; the render-only states (errors, loading, empty, edge cases) are
/// <c>IWidgetPreviewState</c>'s, in <c>Preview/</c>, Debug only, whose <c>sample</c> state calls this.
/// <para>
/// Only a card built for a picture (a preview card, [S-11851]) is ever asked: the sample is held by this
/// view alone, never written into a store, the clipboard or anything another widget or the assistants'
/// server reads. A view whose live sources are the sample world's anyway (the meters and details over the
/// sample PC's hub, [S-1931]) has no sample of its own.
/// </para>
/// </summary>
public interface IWidgetSample
{
    /// <summary>Show the sample day's data; the view stops listening to what it would replace.</summary>
    Task ShowSampleAsync();
}
