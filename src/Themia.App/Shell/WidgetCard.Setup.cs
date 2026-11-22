using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell;

/// <summary>
/// [S-1862] A widget that waits to be set up gets one compact "Set up" line, decided here for every widget by
/// <see cref="WidgetSetup.Needed"/> rather than by each view in its own words.
/// <para>
/// [S-12835] (owner, 2026-11-19, WR-10: "No data, no account, not set up, offline: the widget keeps its face ... and says
/// why in a compact line; it never swaps its face for a status line alone") The line stands on the card's top edge,
/// whole cells (<see cref="LineRules.EdgeRow"/>), and the view keeps its face under it: it runs, set up or not, laid under
/// the line, drawing what it has (its rows empty, its reading "--", its month). A widget whose view draws its own line
/// (<see cref="ISetupRule.SetsUpInPlace"/>: the calendar's in its period line's place, a shortcut that opens nothing yet
/// and takes the drop that sets it up) gets none from the card. The check runs whenever the config arrives, when the
/// view saves a change itself (a target dropped onto it), and when the accounts change. [S-1881] A view parked is one
/// waiting for the device's location ([S-2854]): nothing fetched or drawn for a place it does not have.
/// </para>
/// </summary>
internal sealed partial class WidgetCard
{
    private SetupPrompt? _setup;

    /// <summary>The account service this card listens to, kept so Detach unhooks the one it hooked.</summary>
    private Services.Accounts.AccountService? _setupAccounts;

    /// <summary>Whether the card is showing the Set up button instead of its view.</summary>
    public bool ShowsSetup => _setup is not null;

    /// <summary>[S-1881] The view is built but held back, unattached, because the widget needs setting up.</summary>
    private bool _parked;

    /// <summary>Every account feature signed in now. The card's own service once hooked, the host's before.</summary>
    private FeatureSet SignedIn()
        => WidgetSetup.Offered((_setupAccounts ?? AppServices.Accounts)?.Accounts.Select(a => a.Provider) ?? []);

    private void AttachSetup()
    {
        _setupAccounts = AppServices.Accounts;
        if (_setupAccounts is not null)
        {
            _setupAccounts.AccountsChanged += ApplySetup;
        }

        ApplySetup();
    }

    private void DetachSetup()
    {
        if (_setupAccounts is not null)
        {
            _setupAccounts.AccountsChanged -= ApplySetup;
            _setupAccounts = null;
        }
    }

    /// <summary>[S-2854] The sentence in place of the body while the widget waits for the device's location, and the state it says.</summary>
    private StatusBlock? _waiting;

    private DeviceLocationState? _waitingFor;

    /// <summary>
    /// [S-2853] The config as the view, its title and its sync read it: the widget's own, or, while it
    /// uses the device's location, a copy at the device's position (rounded) named "My location".
    /// The stored config is never touched.
    /// </summary>
    private WidgetConfig ViewConfig => DevicePlaces.Resolve(RowConfig, AppServices.DeviceLocationNow, Str.Shell_Place_MyLocation.Text());

    /// <summary>
    /// [S-15511] The config as the card's row of a repeater shows it (its bound slots set to the row's values; a place
    /// given by the row is a place, so the widget waits for no setup): what its setup, its waiting and its view read.
    /// </summary>
    private WidgetConfig RowConfig => _host.SeriesConfig(this, Model.Config);

    /// <summary>[S-12835] [S-2854] Whether the view runs: set up or not ([S-12835]: it keeps its face), unless it waits for the device's location.</summary>
    private bool Runs(FeatureSet signedIn)
        => WidgetLiveness.Runs(RowConfig, signedIn) && (Unprompted || DevicePlaces.Waits(RowConfig, AppServices.DeviceLocationNow) is null);

    /// <summary>
    /// [S-15529] [S-13913] A container (it draws nothing of its own: its parts speak) and a container's part (its container's
    /// page sets what it lacks) show no Set up row and no waiting row.
    /// </summary>
    private bool Unprompted => Model.Config is IRepeater || _host.HeldByContainer(this);

    /// <summary>
    /// [S-2854] While the widget uses the device's location and Windows gives none, its body says why
    /// (finding it, turned off in Windows settings, not available) over its glyph, the view parked:
    /// nothing is fetched or drawn for a place it does not have, and no other place stands in.
    /// </summary>
    private void ApplyWaiting(DeviceLocationState? waits)
    {
        if (waits == _waitingFor)
        {
            return;
        }

        if (_waiting is not null)
        {
            _content.Children.Remove(_waiting);
            _waiting = null;
        }

        _waitingFor = waits;
        if (waits is { } state)
        {
            // [S-6714] The Status block's row at the top, as the Set up row: Windows' location glyph and the state's sentence.
            _waiting = new StatusBlock { Margin = OverlayTop };
            _waiting.Show(StatusKind.Empty, Glyphs.Location, Config.DevicePlaceRows.Sentence(state));
            _content.Children.Add(_waiting);
        }
    }

    /// <summary>Show or take away the button to match the config and the accounts signed in now.</summary>
    private void ApplySetup()
    {
        var signedIn = SignedIn();
        // [S-1881] [S-12835] A widget set up in place draws its own line (or its own waiting look, taking the drop itself):
        // no line from the card over it.
        var config = RowConfig;
        var needed = !IsFaulted && !Unprompted && WidgetSetup.Needed(config, signedIn) && !WidgetLiveness.SetUpInPlace(config);
        ApplyWaiting(IsFaulted || needed || Unprompted ? null : DevicePlaces.Waits(config, AppServices.DeviceLocationNow));

        if (needed && _setup is null)
        {
            _setup = new SetupPrompt(ModuleCatalog.Of(Model.Kind).Glyph, SetupSentence(signedIn), () => _host.RequestSetup(this));
            _setup.Margin = OverlayTop;
            _content.Children.Add(_setup);
        }
        else if (!needed && _setup is not null)
        {
            _content.Children.Remove(_setup);
            _setup = null;
        }

        // [S-12835] The view stands under the Set up line while there is one.
        ApplyEdges();

        // [S-1881] Run exactly when the widget is useful. Unparking swaps a fresh view in, which runs
        // this check again for the new view, so this pass ends there.
        if (!IsFaulted && Unpark(Runs(signedIn)))
        {
            return;
        }

        // Every time, not only on a change: a retried view arrives fresh, fully visible. [S-12835] The Set up line never
        // covers the face; only the device location's wait parks it.
        var covered = _waiting is not null;
        _view.Opacity = covered ? 0 : 1;
        _view.IsHitTestVisible = !covered;
    }

    /// <summary>
    /// [S-1862] What the Set up row says: the widget's own sentence, else that an account would bring
    /// what it lacks (where Set up goes to settings › Accounts), else what it needs set.
    /// </summary>
    private string SetupSentence(FeatureSet signedIn)
    {
        if (Model.Config is ISetupRule { SetupSentence: { Length: > 0 } own })
        {
            return own;
        }

        var offerable = WidgetSetup.Offered(Themia.Sdk.ProviderCatalog.Offered.Select(p => p.Id));
        return WidgetSetup.Goes(Model.Config, signedIn, offerable) == SetupGoes.Accounts
            ? Str.Shell_Setup_Account.Text()
            : Str.Shell_Setup_Hint.Text();
    }

    /// <summary>
    /// [S-1881] Park a running view that should not run (detach it; it stays, invisible, under the
    /// button), or start a fresh one in place of a parked view that should. True when a fresh view was
    /// swapped in.
    /// </summary>
    private bool Unpark(bool runs)
    {
        if (!runs && _running)
        {
            DetachQuietly();
            _parked = true;
            Log.Debug(Scope, $"widget '{Model.Id}' ({Model.Kind}) needs setting up; its view is stopped");
            return false;
        }

        if (!runs || !_parked)
        {
            return false;
        }

        var parked = _view;
        _view = CreateViewNow(Model.Kind);
        AttachView();
        Swap(parked, _view);
        return true;
    }

#if DEBUG
    /// <summary>[S-6731] Debug renders only: what the card draws for the widget, for the family's render walk: its Set up line and its view.</summary>
    internal IEnumerable<FrameworkElement> FamilyRoots()
    {
        if (_setup is not null)
        {
            yield return _setup;
        }

        yield return _view;
    }

    /// <summary>
    /// [S-15809] Debug renders only: a preview state that draws a config the card would set up (a sun with its place
    /// cleared, <c>IWidgetPreviewState.DrawnConfig</c>) is drawn as the desktop draws it (WR-10, [S-12835]): its face under
    /// the card's compact Set up line, so a render can show the line and the face it keeps.
    /// </summary>
    internal void SetupForPreview(WidgetConfig drawn)
    {
        ArgumentNullException.ThrowIfNull(drawn);
        var signedIn = SignedIn();
        // Only where the state's config is what makes it wait (the card's own does not): a preview runs every account widget
        // past its line on fixture data ([S-2711]), and that stays so.
        if (!_host.IsPreview || _setup is not null || IsFaulted || Unprompted || WidgetSetup.Needed(Model.Config, signedIn)
            || !WidgetSetup.Needed(drawn, signedIn) || WidgetLiveness.SetUpInPlace(drawn))
        {
            return;
        }

        _setup = new SetupPrompt(ModuleCatalog.Of(Model.Kind).Glyph, SetupSentence(signedIn), () => _host.RequestSetup(this));
        _setup.Margin = OverlayTop;
        _content.Children.Add(_setup);
        ApplyEdges();
    }
#endif

    /// <summary>
    /// [S-2711] [S-11850] A preview card only (a render, an add page picture): run the view though the
    /// widget would need setting up, so its sample can draw it. A preview has no accounts, so without
    /// this every account widget is its Set up button. A desktop card is never run past its Set up.
    /// </summary>
    internal void RunForPreview()
    {
        if (!_host.IsPreview)
        {
            return;
        }

        if (_setup is not null)
        {
            _content.Children.Remove(_setup);
            _setup = null;
            ApplyEdges();
        }

        _view.Opacity = 1;
        _view.IsHitTestVisible = true;

        if (_running || _view is not IWidgetView widgetView)
        {
            return;
        }

        _parked = false;
        if (_view is IWidgetSurfaceAware aware)
        {
            aware.Bind(this);
        }

        _running = true;
#if DEBUG
        Gallery.LiveCensus.ViewAttached(_view);
#endif
        widgetView.Attach(_host.Ticker, ViewConfig);

        // The slot was measured while the view was parked, which it did not hear ([S-1881]).
        if (_view is IWidgetSizeAware sizeAware && _view.ActualWidth > 0 && _view.ActualHeight > 0)
        {
            sizeAware.ApplySize(new WidgetArea(_view.ActualWidth, _view.ActualHeight, CellPitch, XamlRoot?.RasterizationScale ?? 1));
        }
    }
}
