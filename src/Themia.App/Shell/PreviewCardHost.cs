using Themia.App.Services;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>
/// [S-1251] [S-11851] A card host with nothing behind it: its cards are previews (inert power, no keys, no
/// widget id), every request is accepted and ignored, nothing is persisted. The add page's pictures build
/// their cards on one, handed the <see cref="Samples"/> they read in place of this PC; the Debug renders'
/// host (<c>RenderCardHost</c>) is one too.
/// </summary>
internal class PreviewCardHost(Ticker ticker) : IWidgetCardHost
{
    private readonly ScreenMetrics _metrics = new(1920, 1080);

    /// <summary>The screen the cards are sized on: the host's own (1920 × 1080 unless set).</summary>
    public ScreenMetrics Metrics
    {
        get => Screen ?? _metrics;
        init => _metrics = value;
    }

    /// <summary>A screen that stands in for <see cref="Metrics"/> while there is one (a render's wallpaper).</summary>
    protected virtual ScreenMetrics? Screen => null;

    public virtual Backdrop? Backdrop => null;

    /// <summary>[S-11851] What these cards read in place of this PC; null reads the host's as a render does.</summary>
    public SampleWorld? Samples { get; init; }

    public DesignState Design { get; init; } = new();

    public Ticker Ticker => ticker;

    /// <summary>[S-3713] Edit mode, for the selection page's render; false elsewhere.</summary>
    public bool EditMode { get; init; }

    /// <summary>[S-3713] The ids drawn selected; none elsewhere.</summary>
    public IReadOnlySet<string> Selected { get; init; } = new HashSet<string>();

    public ViewState? ViewForCards => null;

    /// <summary>[S-3300] Every card of a preview is on one stand-in view, so a linked state shows on each.</summary>
    public Themia.Core.Links.LinkBoard Links { get; init; } = new();

    public bool CanPaste => false;

    public bool IsPreview => true;

    public void OnGeometryPreview(WidgetCard card)
    {
        // ignore: a preview does not move cards
    }

    public void OnGeometryCommitted(WidgetCard card)
    {
        // ignore: as above
    }

    public void RequestSetup(WidgetCard card)
    {
        // ignore: a preview has no edit mode to enter and no menu to open
    }

    public bool RequestAccounts() => false;

    public void RequestDelete(WidgetCard card)
    {
        // ignore: a fixed set of cards
    }

    public void RequestEditMode(bool enabled)
    {
        // ignore: a preview's edit mode is fixed
    }

    public bool IsSelected(WidgetCard card) => Selected.Contains(card.Model.Id);

    /// <summary>[S-14310] The ids a render's selection frame goes round (two or more, <see cref="SelectionRules.Framed{T}"/>); none elsewhere.</summary>
    public IReadOnlySet<string> Framed { get; set; } = new HashSet<string>();

    public int FramedWith(WidgetCard card) => Framed.Contains(card.Model.Id) ? Framed.Count : 0;

    public void RequestSelect(WidgetCard card)
    {
        // ignore: nothing to select into
    }

    public void RequestCopy(WidgetCard card)
    {
        // ignore: no clipboard here
    }

    public void RequestCut(WidgetCard card)
    {
        // ignore: as above
    }

    public void RequestPaste()
    {
        // ignore: as above
    }

    public void RequestSaveConfig()
    {
        // ignore: nothing is persisted
    }

    public void RequestTextInput(WidgetCard card, bool wanted)
    {
        // ignore: no keyboard here
    }

    public void KeyboardPress(WidgetCard card)
    {
        // ignore: as above
    }
}
