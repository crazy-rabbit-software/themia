using Microsoft.UI.Xaml;
using Themia.App.Services.Web;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5160] [S-7042] What the online widgets share: their one answer as the kit's one stale rule
/// reads it. While the answer on screen is one the last attempt could not improve on, the status's
/// problem glyph (<c>KitStatus</c>, [S-12481]) says so ("No network · updated 10:42"); the card menu's
/// Refresh line says how old it is. The age itself is never drawn on the widget. With no answer at all, the
/// state is the Status block's row at the top of the content ([S-6734]).
/// </summary>
public static class OnlineParts
{
    /// <summary>How the answer last went, as a kit source's outcome (one source, no owner).</summary>
    public static KitOutcome Outcome<T>(Fetched<T> held)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(held);
        return new KitOutcome(null, held.Status, null, held.Value is null ? null : held.At);
    }

    /// <summary>The status's problem lines: none while the answer on screen is the last attempt's, or nothing is on screen.</summary>
    public static IReadOnlyList<string> Problems<T>(Fetched<T>? held)
        where T : class
        => held is { Value: not null } ? KitFreshness.Lines(Strings.Current, [Outcome(held)], DateTimeOffset.Now) : [];

    /// <summary>
    /// [S-15711] The status's problem lines while nothing is on screen: the sentence for <paramref name="status"/> where it is a
    /// problem (<see cref="FetchState.Look"/>: out of reach, timed out, refused), so a failure is never quieter than a stale
    /// answer; none while not set up (the card's Set up row says it), loading, offline or empty.
    /// </summary>
    public static IReadOnlyList<string> Failure(FetchStatus status)
        => FetchState.Look(status).Kind == StatusKind.Problem ? [FetchState.Message(status)] : [];

    /// <summary>The card menu's Refresh line: "Refresh · updated 4 min ago", or "Refresh" while nothing is held.</summary>
    public static string RefreshLabel<T>(Fetched<T>? held)
        where T : class
        => KitFreshness.RefreshLabel(Strings.Current, held is null ? [] : [Outcome(held)], DateTimeOffset.Now);

    /// <summary>
    /// [S-12813] No answer to draw: the readout stays, "--" (with <paramref name="unit"/>) and disabled, the reason its
    /// line under (the sentence for <paramref name="status"/>, or <paramref name="sentence"/>), never a status row in
    /// its place. Not set up says nothing more: the card's Set up row says it ([S-1862]).
    /// </summary>
    public static void ShowDefault(ReadoutBlock readout, FetchStatus status, string unit = "", string? sentence = null)
    {
        ArgumentNullException.ThrowIfNull(readout);
        var caption = sentence ?? Caption(status);
        readout.Show("--", unit, caption, valueRole: ColourRole.TextDisabled);
        readout.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// [S-6753] A graph's caption with no answer to draw: the sentence for <paramref name="status"/> written small with
    /// the chart at its baseline, never in its place; not set up says nothing (the card's Set up row says it).
    /// </summary>
    public static string Caption(FetchStatus status)
        => FetchState.Look(status).Kind == StatusKind.Setup ? string.Empty : FetchState.Message(status);

    /// <summary>
    /// [S-6734] Say why there is no answer in <paramref name="row"/>: the sentence for <paramref name="status"/>
    /// (<see cref="FetchState.Message(FetchStatus)"/>), led by the widget's <paramref name="glyph"/> (the
    /// warning for a problem), with Retry running <paramref name="retry"/> where <see cref="FetchState.Look"/>
    /// offers it. Not set up shows nothing here: the card's Set up row says it ([S-1862]).
    /// </summary>
    public static void ShowState(StatusBlock row, FetchStatus status, string glyph, Action retry)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(retry);
        var look = FetchState.Look(status);
        if (look.Kind == StatusKind.Setup)
        {
            row.Hide();
            return;
        }

        row.Show(look.Kind, glyph, FetchState.Message(status), look.OffersRetry ? Str.Common_Retry.Text() : null, look.OffersRetry ? retry : null);
    }
}
