using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Geometry;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Windows.System;

namespace Themia.App.Shell;

/// <summary>
/// [S-2325]..[S-2327] The edit bar's flyouts on this display, one mechanism for every one of them: a page's
/// entry, or the selection's, opens its flyout hung from it; one open at a time; it closes on its entry
/// again, Escape (after going back out of what it opened), a press anywhere that is not the bar or the
/// flyout, the add menu opening, a placement, and leaving edit mode. Nothing is open as edit mode starts.
/// [S-13715] The Unplaced icons page alone stays open under a press outside it, so the desktop is used beside it.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-2325] The flyouts' layer: over the cards and the edit bar, after it in the keyboard's order.</summary>
    private readonly Canvas _flyoutLayer = new();

    /// <summary>The open flyout, if any.</summary>
    private PageFlyout? _flyout;

    /// <summary>
    /// [S-2325]..[S-2327] The one flyout of the bar open on this display (a page's, the selection's, the Unplaced icons', the
    /// add page, the gallery), as one value: set by <see cref="Become"/> alone.
    /// </summary>
    private OpenFlyout _open = OpenFlyout.None;

    /// <summary>[S-2320] The bar is held: a step under way shows it once, as it ends (<see cref="Step"/>).</summary>
    private int _barHeld;

    /// <summary>[S-2320] The bar was asked to show while held.</summary>
    private bool _barAsked;

    /// <summary>[S-2327] The one transition of the bar's open flyout: <paramref name="next"/> is what is open now, and the bar shows it.</summary>
    private void Become(OpenFlyout next)
    {
        _open = next;
        ShowEditBar();
    }

    /// <summary>
    /// [S-2320] A step of the bar's flyouts (one closing, another opening, the selection's page following it): the bar is
    /// shown once as the step ends, whatever it asked on the way, not again by every flyout it closes and opens.
    /// </summary>
    private void Step(Action step)
    {
        _barHeld++;
        try
        {
            step();
        }
        finally
        {
            if (--_barHeld == 0 && _barAsked)
            {
                _barAsked = false;
                ShowEditBar();
            }
        }
    }

    /// <summary>
    /// [S-2346] The open flyout drawn again: never under a press held on it, never inside the event that asked;
    /// made with the first flyout, when the surface's dispatcher is there.
    /// </summary>
    private FlyoutRedraws? _redraws;

    /// <summary>[S-2327] Whether one of the edit bar's flyouts is open on this display.</summary>
    internal bool BarMenuOpen => _open.IsOpen;

    /// <summary>[S-2300] A page's rows, as the state and Windows hold them now.</summary>
    private Func<PageModel> PageOf(SettingsPageId page)
    {
        var settings = new SurfaceSettingsHost(this);
        var display = new SurfaceDisplayHost(this);
        var views = new SurfaceViewsHost(this);
        return page switch
        {
            SettingsPageId.General => () => PageRows.Of(SettingsPages.General(settings)),
            SettingsPageId.Accounts => () => PageRows.Of(SettingsPages.Accounts(settings)),
            SettingsPageId.Display => () => PageRows.Of(SettingsPages.Display(display)),
            SettingsPageId.Views => () => PageRows.Of(SettingsPages.Views(views)),

            // [S-2311] Its preview: this display's wallpaper and real cards in the design as it stands; [S-2364] kept.
            _ => () => PageRows.Of(SettingsPages.Appearance(settings), KeptPreview),
        };
    }

    /// <summary>
    /// [S-2364] The Appearance page's preview, kept from one opening of the page to the next while edit mode lasts: its
    /// cards were built on every opening (half of what opening the page cost). Built again for another display size or
    /// language; let go as edit mode ends and with the surface.
    /// </summary>
    private AppearancePreview? _keptPreview;

    /// <summary>[S-2364] The language the kept preview's cards speak.</summary>
    private Core.Localization.Locale? _keptPreviewLocale;

    /// <summary>[S-2364] The page the kept preview stands on now.</summary>
    private AppearancePreview.Kept? _keptPreviewShown;

    /// <summary>[S-2364] The kept preview for a page that opens, built where there is none for this display's size and language.</summary>
    private AppearancePreview.Kept KeptPreview()
    {
        var metrics = Metrics;
        if (_keptPreview is { } kept && (kept.Metrics.Width != metrics.Width || kept.Metrics.Height != metrics.Height || _keptPreviewLocale != Core.Localization.Strings.Current))
        {
            DropKeptPreview();
        }

        // A page before this one still holding it lets go first.
        _keptPreviewShown?.Dispose();
        _keptPreview ??= new AppearancePreview(Backdrop, metrics, () => _host.State.Design);
        _keptPreviewLocale = Core.Localization.Strings.Current;
        _keptPreviewShown = new AppearancePreview.Kept(_keptPreview);
        return _keptPreviewShown;
    }

    /// <summary>[S-2364] The kept preview goes, its cards and samples with it.</summary>
    private void DropKeptPreview()
    {
        _keptPreviewShown?.Dispose();
        _keptPreviewShown = null;
        _keptPreview?.Dispose();
        _keptPreview = null;
    }

    /// <summary>[S-2351] An "are you sure?" asked after a step of its own, in place in the card of the open flyout's row keyed <paramref name="at"/>.</summary>
    private void ConfirmOnFlyout(string at, SettingsConfirm confirm) => _flyout?.Page.ConfirmAt(at, PageRows.QuestionOf(confirm));

    /// <summary>[S-2327] A page's entry opens its flyout, or closes it again; another open closes first.</summary>
    private void TogglePage(SettingsPageId page) => Step(() =>
    {
        if (_open.PageId == page)
        {
            CloseFlyout(focusEntry: false);
        }
        else
        {
            OpenPage(page, []);
        }
    });

    /// <summary>
    /// [S-2327] [S-3534] Open a page's flyout on this display at the row along <paramref name="path"/> (a
    /// section's key, then the rows'; empty for its top), the keyboard in it. Only in edit mode and not
    /// while placing: the bar is not there otherwise.
    /// </summary>
    internal void OpenPage(SettingsPageId page, IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        Step(() => OpenPageAt(page, path));
    }

    private void OpenPageAt(SettingsPageId page, IReadOnlyList<string> path)
    {
        if (_closed || !EditMode || _host.Placing is not null)
        {
            return;
        }

        // [S-16727] Unplaced icons is the widget gallery's Not on view tab now: a way to its page opens the gallery there.
        if (page == SettingsPageId.DesktopItems)
        {
            OpenWidgetGallery();
            _widgetGallery?.ShowUnplaced();
            return;
        }

        if (_open.PageId != page)
        {
            CloseFlyout(focusEntry: false);
            Show(new PageView(PageOf(page)));
            _open = OpenFlyout.Of(page);
            if (page is SettingsPageId.General or SettingsPageId.Accounts)
            {
                _host.SettingsMenuOpened();
            }
        }

        _flyout!.Page.Show();
        ShowEditBar();
        PlaceFlyout();
        if (path.Count > 0 ? !_flyout.Page.Open(path) : !_flyout.Page.FocusFirst())
        {
            Core.Diagnostics.Log.Debug(Scope, $"the {page} page opened, but not at '{string.Join('/', path)}'");
        }

        PlaceFlyout();
    }

    /// <summary>[S-2343] The selection's flyout: its page built over the selection as it is now.</summary>
    private void OpenSelectionPage(bool focus) => Step(() => OpenSelectionPageNow(focus));

    private void OpenSelectionPageNow(bool focus)
    {
        // [S-14916] Widgets placed off the gallery land selected under it: the gallery stays, their page does not open over it.
        if (_closed || !EditMode || _host.Placing is not null || !_selectionOffer.Shows || _widgetGallery is not null)
        {
            return;
        }

        // [S-13715] The Unplaced icons page stays while the desktop is used beside it: a selection made there opens its page from
        // its entry (focus: the bar's entry asked), never on its own over the page.
        if (!focus && !_open.IsSelection && _open.StaysOpen)
        {
            return;
        }

        if (!_open.IsSelection)
        {
            CloseFlyout(focusEntry: false);
            Show(new PageView(SelectionModel));
            _open = OpenFlyout.Selection;
        }

        _flyout!.Page.Show();
        ShowEditBar();
        PlaceFlyout();
        if (focus && !_flyout.Page.FocusFirst())
        {
            Core.Diagnostics.Log.Debug(Scope, "the selection's page opened with nothing to focus");
        }
    }

    /// <summary>The flyout of <paramref name="page"/> put on its layer.</summary>
    private void Show(PageView page)
    {
        UiLanguage.ApplyFlow(page);
        _flyout = new PageFlyout(page);
        _flyout.KeyDown += OnFlyoutKey;
        if (_redraws is null)
        {
            _redraws = new FlyoutRedraws(_root.DispatcherQueue, DrawBarMenu);

            // A release anywhere on the surface lets go too: a press dragged off the page before it let go.
            var redraws = _redraws;
            _root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => redraws.LetGo()), handledEventsToo: true);
        }

        _redraws.Reset();
        _flyout.Pressed += _redraws.Pressed;
        _flyout.LetGo += _redraws.LetGo;

        // [S-2350] Hung again whenever its page grows or shrinks.
        _flyout.PageResized += PlaceFlyout;
        _flyoutLayer.Children.Add(_flyout);
    }

    /// <summary>[S-2327] Close the open flyout; the keyboard back on its entry where it was closed from the keyboard.</summary>
    private void CloseFlyout(bool focusEntry) => Step(() => CloseFlyoutNow(focusEntry));

    private void CloseFlyoutNow(bool focusEntry)
    {
        // [S-14910] [S-16728] The gallery (Add widget's) is one of the bar's flyouts: one at a time, closed with the rest.
        CloseWidgetGallery();
        if (_flyout is not { } flyout)
        {
            return;
        }

        var page = _open.PageId;
        var selection = _open.IsSelection;
        flyout.Page.Close();
        if (_redraws is { } redraws)
        {
            flyout.Pressed -= redraws.Pressed;
            flyout.LetGo -= redraws.LetGo;
            redraws.Reset();
        }

        _flyoutLayer.Children.Remove(flyout);
        _flyout = null;
        if (page is SettingsPageId.General or SettingsPageId.Accounts)
        {
            _host.SettingsMenuClosed();
        }

        Become(OpenFlyout.None);
        if (focusEntry)
        {
            _editBar.FocusEntry(selection ? null : page);
        }
    }

    /// <summary>
    /// [S-3593] Edit mode ended: the open flyout and the bar's buttons (with the add menu) are dropped;
    /// each is built again as it is next needed.
    /// </summary>
    private void ReleaseEditMenus()
    {
        CloseFlyout(focusEntry: false);
        DropKeptPreview();
        _selectionShown = null;
        _editBar.Release();
    }

    /// <summary>
    /// [S-3531] The open flyout shows the state as it is now (an undo, an import, another display's change).
    /// [S-2346] Drawn after the event that asked, never under a press held on it: the row pressed would be gone by
    /// the release, and taking it away inside the pointer's own event throws out of it.
    /// </summary>
    private void SyncBarMenu()
    {
        if (!_closed && _flyout is not null)
        {
            _redraws?.Ask();
        }
    }

    /// <summary>[S-2346] The posted drawing's turn: the open flyout drawn again and hung again.</summary>
    private void DrawBarMenu()
    {
        if (!_closed && _flyout is { } flyout)
        {
            flyout.Page.Show();
            PlaceFlyout();
        }
    }

    /// <summary>
    /// [S-2327] A press anywhere on the surface that is not the bar or the open flyout closes it, and the
    /// press goes on to do what it does. [S-2343] A press on a widget leaves the selection's page open:
    /// a click on a widget ends with its page open, so closing it only to open it again would flash;
    /// it goes when the widget starts to move (<see cref="ClearForDrag"/>). [S-13715] The Unplaced icons page has no light
    /// dismiss (owner, 2026-11-21: "it should not close when i click outside of it"): the press goes on, the page stays.
    /// </summary>
    private void OnPressForBarMenu(object sender, PointerRoutedEventArgs e)
    {
        FrameworkElement? open = _flyout;
        if (open is null || e.OriginalSource is not DependencyObject source || (_flyout is not null && _open.StaysOpen))
        {
            return;
        }

        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, open) || ReferenceEquals(node, _editBar) || (_open.IsSelection && node is WidgetCard))
            {
                return;
            }
        }

        CloseFlyout(focusEntry: false);
    }

    /// <summary>[S-2327] Escape goes back out of what the flyout opened, then closes it; Page Up / Down, Home and End scroll it.</summary>
    private void OnFlyoutKey(object sender, KeyRoutedEventArgs e)
    {
        if (_flyout is not { } flyout)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Escape:
                if (!flyout.Page.Back())
                {
                    CloseFlyout(focusEntry: true);
                }

                e.Handled = true;
                break;
            case VirtualKey.PageUp or VirtualKey.PageDown or VirtualKey.Home or VirtualKey.End when !TextHasFocus():
                e.Handled = flyout.Scroll(e.Key);
                break;
        }
    }

    /// <summary>[S-2327] Down from the bar: the keyboard into the open flyout.</summary>
    private void FocusFlyout()
    {
        if (_flyout is { } flyout && !flyout.Page.FocusFirst())
        {
            Core.Diagnostics.Log.Debug(Scope, "the open flyout has nothing to focus");
        }
    }

    /// <summary>
    /// [S-2325] Hung from its entry: centred on it, 12 under the bar, inside the work area, as tall as its page up to 70 % of it;
    /// [S-13707] a page as wide as the bar (<see cref="SettingsPages.BarWide"/>) the bar's width, still pointing at its entry.
    /// </summary>
    private void PlaceFlyout()
    {
        if (_closed || _flyout is not { } flyout)
        {
            return;
        }

        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var area = WorkArea;
        var bar = EditBarRect(area);
        var entry = _editBar.EntryBox(_open.PageId, _flyoutLayer) ?? new DipRect(bar.X, bar.Y, bar.Width, bar.Height);

        // [S-13707] The Unplaced icons page is as wide as the bar, as the add page is: measured at that width.
        flyout.Place(_open.PageId is { } page && SettingsPages.BarWide(page)
            ? FlyoutPlacement.Wide(entry, bar, area, flyout.ContentHeightAt(bar.Width))
            : FlyoutPlacement.Place(entry, bar, area, flyout.ContentHeight));
        ShineSelection();
    }

    /// <summary>
    /// [S-2344] The selection's page lets the selected widgets it covers be seen through it, as their
    /// cards are drawn now; every other page covers what is under it.
    /// </summary>
    private void ShineSelection()
    {
        if (_flyout is not { } flyout)
        {
            return;
        }

        var under = new List<DipRect>();
        if (_open.IsSelection)
        {
            foreach (var card in _cards)
            {
                if (_host.Selection.Contains(card.Model.Id) && card.Visibility == Visibility.Visible && card.Width > 0 && card.Height > 0)
                {
                    under.Add(new DipRect(Canvas.GetLeft(card), Canvas.GetTop(card), card.Width, card.Height));
                }
            }
        }

        flyout.ShineThrough(under);
    }
}
