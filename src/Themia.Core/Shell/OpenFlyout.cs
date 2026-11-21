using Themia.Core.Geometry;
using Themia.Core.Settings;

namespace Themia.Core.Shell;

/// <summary>
/// [S-2325]..[S-2327] [S-11707] [S-14910] The one flyout of the edit bar open on a display, as one value: none, a settings
/// page's, the selection's, the Unplaced icons page's, the add page (into a lassoed area or not) or the widget gallery. One
/// at a time: opening one is a step from the one open to it. What the bar marks as open is read from it.
/// </summary>
public abstract record OpenFlyout
{
    private OpenFlyout()
    {
    }

    /// <summary>Nothing open.</summary>
    public static OpenFlyout None { get; } = new Nothing();

    /// <summary>[S-2343] The selection's page.</summary>
    public static OpenFlyout Selection { get; } = new SelectionPage();

    /// <summary>[S-14910] The widget gallery.</summary>
    public static OpenFlyout Gallery { get; } = new WidgetGallery();

    /// <summary>[S-13700] The Unplaced icons page: a settings page of its own that stays open under a press outside it ([S-13715]).</summary>
    public static OpenFlyout Unplaced { get; } = new UnplacedPage();

    /// <summary>[S-2327] <paramref name="page"/>'s flyout: [S-13700] the Unplaced icons page's own value for its page.</summary>
    public static OpenFlyout Of(SettingsPageId page) => page == SettingsPageId.DesktopItems ? Unplaced : new Page(page);

    /// <summary>[S-11707] [S-13018] The add page, placing into <paramref name="area"/> (a lassoed area of the display) or after the pointer.</summary>
    public static OpenFlyout Adding(FracRect? area = null) => new Add(area);

    /// <summary>Whether a flyout is open.</summary>
    public bool IsOpen => this is not Nothing;

    /// <summary>The settings page whose flyout is open (the Unplaced icons page's too); null for any other.</summary>
    public SettingsPageId? PageId => this switch
    {
        Page page => page.Id,
        UnplacedPage => SettingsPageId.DesktopItems,
        _ => null,
    };

    /// <summary>[S-2343] The selection's page is open.</summary>
    public bool IsSelection => this is SelectionPage;

    /// <summary>[S-11707] The add page is open.</summary>
    public bool IsAdd => this is Add;

    /// <summary>[S-14910] The widget gallery is open.</summary>
    public bool IsGallery => this is WidgetGallery;

    /// <summary>[S-13018] The lassoed area the open add page places into; null for none.</summary>
    public FracRect? AddArea => (this as Add)?.Area;

    /// <summary>A page of the bar's flyout layer stands open (a settings page's, the Unplaced icons', the selection's).</summary>
    public bool HoldsPage => this is Page or UnplacedPage or SelectionPage;

    /// <summary>[S-13715] It stays open under a press outside it (the Unplaced icons page alone).</summary>
    public bool StaysOpen => SettingsPages.StaysOpen(PageId);

    private sealed record Nothing : OpenFlyout;

    private sealed record Page(SettingsPageId Id) : OpenFlyout;

    private sealed record UnplacedPage : OpenFlyout;

    private sealed record SelectionPage : OpenFlyout;

    private sealed record Add(FracRect? Area) : OpenFlyout;

    private sealed record WidgetGallery : OpenFlyout;
}
