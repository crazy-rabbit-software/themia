namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] The quote's sample: the rotation's quote of the day pinned to the bundled Newton
/// (a sentence of a typical length), so the picture is the same on every day.
/// </summary>
internal sealed partial class QuoteView : IWidgetSample
{
    /// <summary>The bundled quote the sample pins.</summary>
    internal const int SampleQuote = 3;

    public Task ShowSampleAsync()
    {
        _pinned = SampleQuote;
        Render();
        return Task.CompletedTask;
    }
}
