using Themia.App.Widgets;
using Themia.Core.Diagnostics;

namespace Themia.App.Shell;

/// <summary>
/// [S-11850] [S-11852] Cards of a preview host shown on their samples, one after another: a card waiting to be set up
/// runs past its Set up for the preview (<see cref="WidgetCard.RunForPreview"/>), then each view that has a sample shows
/// it (<see cref="IWidgetSample"/>); one without draws what its host hands it (the sample PC's readings). The one way the
/// live previews (the Appearance page's, the widget gallery's) show their cards' samples.
/// </summary>
internal static class SampleCards
{
    /// <summary>
    /// Show each of <paramref name="cards"/>' samples in turn; <paramref name="stopped"/> ends the run early (the preview went).
    /// [S-15900] A load that draws later: announced (<see cref="Services.LoadWatch"/>), a render settles once it is done.
    /// </summary>
    public static Task ShowAsync(IReadOnlyList<WidgetCard> cards, Func<bool> stopped, string scope)
        => Services.LoadWatch.Of(RunAsync(cards, stopped, scope));

    private static async Task RunAsync(IReadOnlyList<WidgetCard> cards, Func<bool> stopped, string scope)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(stopped);
        foreach (var card in cards)
        {
            if (stopped())
            {
                return;
            }

            try
            {
                if (card.ShowsSetup)
                {
                    card.RunForPreview();
                }

                if (card.View is IWidgetSample sample)
                {
                    await sample.ShowSampleAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(scope, $"the {card.Model.Kind} card's sample could not be shown; it shows what it has", ex);
            }
        }
    }
}
