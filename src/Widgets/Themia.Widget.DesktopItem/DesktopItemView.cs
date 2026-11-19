using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5600]..[S-5609] [S-9500] One desktop icon as Explorer's desktop draws one, for both sources (a
/// desktop item, an own icon): its icon and its name (legible over any wallpaper, [S-6733]) in the
/// desktop's one cell (<see cref="DesktopCell"/>) and its one icon layout ([S-13970]: the name below the icon,
/// beside it, or shown on hover alone), in the desktop icon's selection look
/// (<see cref="SelectionLook.DesktopIcon"/>, [S-6627]), on no card.
/// <para>
/// One behaviour for both: every gesture is the desktop's ([S-5540]..[S-5549]). A press, a key, a
/// typed letter, the right button, a double-click, a drag out and F2's commit are handed to the host's one
/// desktop selection (<see cref="IWidgetHost.DesktopItems"/>) under the icon's key
/// (<see cref="DesktopItemKeys.Of"/>), which paints back what it decided. A press that moves is its
/// card's move, as every widget's ([S-2008], [S-11878]): the card hands it back here (<see cref="DragOut"/>)
/// only to leave the desktop. The view has no selection, no menu and no command of its own; F2's box is
/// drawn here.
/// </para>
/// <para>
/// <b>An own icon's target is re-checked at the moment it is opened</b>, by the host's launcher. The
/// config it came from lives in the user's profile and anything running as the user can rewrite it,
/// so a target that was valid when it was set is not evidence that it is valid now. A target the rule
/// refuses is drawn as refused rather than quietly dropped, and looking at a target (its icon,
/// whether it is still there) is gated too: a network path is never probed unasked ([S-9504]).
/// </para>
/// <para>
/// Split by concern: this file owns the lifecycle, the gestures, the drawing and the rename box;
/// <c>.Own.cs</c> an own icon's checks and tooltip ([S-9504], [S-9510]); <c>.Icon.cs</c> the icon
/// ([S-5609], [S-9503]); <c>.Drop.cs</c> the drops ([S-5587], [S-9508]); <c>.Links.cs</c> its view's links;
/// <c>.Layout.cs</c> the icon layout and icon only's name past the cell ([S-13970]..[S-13975]).
/// </para>
/// </summary>
internal sealed partial class DesktopItemView : Grid, IWidgetView, IWidgetKeys, IWidgetSurfaceAware, IWidgetDropTarget, IWidgetTopEdge, IDesktopIconView
{
    private const string Scope = "desktop-item";

    private readonly IWidgetHost _host;
    private readonly Grid _iconHost = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Image _image = new() { Stretch = Stretch.Uniform };

    /// <summary>[S-5593] The desktop's icon size this view is drawn at: the one in force when it last rendered.</summary>
    private DesktopIconSize _size = DesktopCell.Current;
    private readonly WallpaperLabel _name = new(FamilyType.Caption);

    /// <summary>The icon and the name, inside the cell's insets: what the cell draws, laid out for the icon layout (<see cref="Relook"/>).</summary>
    private readonly StackPanel _stack = new();

    /// <summary>[S-5612] The highlight behind the icon and the name: the whole card box, where its corners are (<see cref="DesktopCell.Highlight"/>).</summary>
    private readonly Border _chrome = SelectionChrome.Wrap(null, SelectionLook.DesktopIcon);

    private IWidgetSurface? _surface;
    private Ticker? _ticker;
    private DesktopItemConfig _config = new();
    private bool _attached;
    private IDisposable? _hover;
    private bool _hovered;

    /// <summary>[S-5605] What a debug render's preview state sets (never set on the desktop).</summary>
    private readonly PreviewFixture _fixture = new();

    /// <summary>[S-6621] A left press waiting for its release (a move takes the pointer to the card instead), and whether Ctrl or Shift was held (a modified click selects and never opens).</summary>
    private bool _pressed;
    private bool _pressModified;

    /// <summary>[S-5607] The rename box while it shows, and whether it is being taken down (its LostFocus then commits nothing).</summary>
    private TextBox? _box;
    private bool _closingBox;

    /// <summary>[S-5608] What was last announced: a change of either is told to a screen reader.</summary>
    private bool _toldSelected;
    private bool _toldFocused;

    public DesktopItemView(IWidgetHost host)
    {
        _host = host;
        _stack.Children.Add(_iconHost);

        // [S-5564] A picture decoded after it was set (a file, a site's icon) is drawn only now: the ghost's copy follows.
        _image.ImageOpened += (_, _) => KeepDragIcon();
        BuildOverhang();

        // [S-5612] The highlight is the whole card box (the view owns the card's top edge, [S-5700], so its box is the card's).
        // [S-13974] Icon only's name stands under the icon, which is centred in the box.
        SizeChanged += (_, args) =>
        {
            PlaceChrome(DesktopCell.Highlight(args.NewSize.Width, args.NewSize.Height));
            PlaceOverhang();
        };
        Relook(_size, _layout);
        Children.Add(_chrome);
        Children.Add(_stack);

        // Clear, not null: the empty part of the cell takes the press too.
        Background = Fluent.Clear();
        FollowLinks();
    }

    /// <summary>[S-5612] Put the highlight on <paramref name="box"/>, in the view's (the card's) DIPs.</summary>
    private void PlaceChrome(Themia.Core.Geometry.PixelRect box)
    {
        _chrome.HorizontalAlignment = HorizontalAlignment.Left;
        _chrome.VerticalAlignment = VerticalAlignment.Top;
        _chrome.Margin = new Thickness(box.X, box.Y, 0, 0);
        _chrome.Width = box.Width;
        _chrome.Height = box.Height;
    }

    /// <summary>[S-5700] The cell is the whole card, from its top edge: the card adds no inset above it (one would push the name's second line out of the cell).</summary>
    public bool OwnsTopEdge => true;

    /// <summary>[S-5564] The icon's square: where a drag grabbed the icon is measured from it.</summary>
    public FrameworkElement DragIconBox => _iconHost;

    /// <summary>[S-5564] The icon's picture, kept current (<c>.Icon.cs</c>): what a drag's ghost shows of this icon.</summary>
    public GhostIcon? DragIcon => _dragIcon;

    /// <summary>Whether the icon drawn is an own icon; a preview's own item is always a desktop item.</summary>
    private bool Own => _fixture.Item is null && !_config.IsDesktopItem;

    /// <summary>The desktop item drawn: the preview's own, else the host's listing's, else one named from the key alone ([S-5603]); null for an own icon.</summary>
    private Themia.Sdk.DesktopItem? Item
    {
        get
        {
            if (_fixture.Item is not null)
            {
                return _fixture.Item;
            }

            if (!_config.IsDesktopItem)
            {
                return null;
            }

            return _host.DesktopItems.Find(_config.Item)
                ?? new Themia.Sdk.DesktopItem(_config.Item, DesktopItemKeys.FallbackName(_config.Item), DesktopItemKind.File);
        }
    }

    /// <summary>[S-5540] The key the icon is selected under, whichever its source.</summary>
    internal string Key => _fixture.Item?.Key ?? DesktopItemKeys.Of(_config);

    /// <summary>The name drawn under the icon.</summary>
    internal string ShownName => Own ? OwnIconRules.Name(_config) : Item?.Name ?? string.Empty;

    /// <summary>Whether gestures act: on the desktop, outside edit mode, never in a preview.</summary>
    internal bool Acts => _attached && _surface?.EditMode != true && _surface?.IsPreview != true && !_fixture.On && !_host.IsPreview;

    /// <summary>[S-5608] The host, for the icon's automation peer.</summary>
    internal IWidgetHost Host => _host;

    /// <summary>[S-5584] Whether F2 can rename it: an own icon always (its label), a desktop item by the shell's rule (never a shell item).</summary>
    internal bool Renamable => Own || (Item is { } item && DesktopRename.CanRename(item));

    /// <summary>[S-5608] A screen reader sees the icon as a list item of the desktop's list.</summary>
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new DesktopItemAutomationPeer(this);

    private string? WidgetId => _surface?.WidgetId;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Detach();
        _attached = true;
        _ticker = ticker;
        _host.DesktopItems.Changed += OnDeskChanged;
        Strings.Changed += OnLanguage;
        PointerPressed += OnPressed;
        PointerReleased += OnReleased;
        PointerCaptureLost += OnPressLost;
        PointerCanceled += OnPressLost;
        Tapped += OnTapped;
        DoubleTapped += OnDoubleTapped;
        RightTapped += OnRightTapped;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _hover = SelectionChrome.Hover(this, hovered =>
        {
            _hovered = hovered;
            Paint();
            PointLink(hovered);
            if (hovered)
            {
                // [S-5610] The tip is made for the pointer: whether the name is cut is known now, and a file's facts are asked now.
                PaintTip();
                _ = LoadFactsAsync();
            }
        });
        _links.Join(_surface?.Links);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as DesktopItemConfig ?? new DesktopItemConfig();
        Changed();
    }

    /// <summary>The config changed (the page, a drop, a rename, the reconcile): an own icon's target is classified again, its checks follow it, and the icon is drawn anew.</summary>
    private void Changed()
    {
        _target = Own && !_config.IsLayout ? LaunchTargets.Classify(_config.Target) : default;
        _health = TargetHealth.Unknown;
        _facts = null;
        _factsRequest++;
        UpdateSubscriptions();
        Render();
    }

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        _host.DesktopItems.Changed -= OnDeskChanged;
        Strings.Changed -= OnLanguage;
        PointerPressed -= OnPressed;
        PointerReleased -= OnReleased;
        PointerCaptureLost -= OnPressLost;
        PointerCanceled -= OnPressLost;
        Tapped -= OnTapped;
        DoubleTapped -= OnDoubleTapped;
        RightTapped -= OnRightTapped;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        WatchScale(null);
        _hover?.Dispose();
        _hover = null;
        _healthTick?.Dispose();
        _healthTick = null;
        _healthGeneration++;
        WatchBin(false);
        _pressed = false;
        CloseBox();
        _iconRequest++;
        _dragIconRequest++;
        _factsRequest++;
        _links.Leave();
    }

    /// <summary>The listing, the selection, a rename box or a drag's target changed: a desktop item may have a new name or icon, and both sources a new look.</summary>
    private void OnDeskChanged()
    {
        if (Own && _size == DesktopCell.Current && _layout == DesktopCell.CurrentLayout)
        {
            Paint();
            SyncBox();
        }
        else
        {
            // A desktop item may have a new name or icon; either source another icon size or layout ([S-5593], [S-13970]).
            Render();
        }
    }

    /// <summary>The language changed: the name of a location, a Settings page or an unnamed layout, and the tooltip, are words too.</summary>
    private void OnLanguage(Locale locale) => Render();

    // ---- keys ([S-5541], [S-5545]) -------------------------------------------------------------------

    /// <summary>
    /// The keys with the lease are the desktop's (the arrows by place, Enter, F2, Delete, the clipboard,
    /// the Menu key); Escape is the lease's own end ([S-813]), which keeps the selection.
    /// </summary>
    public bool HandleKey(WidgetKey key)
        => key != WidgetKey.Escape && Acts && _host.DesktopItems.Key(key, SelectionInput.ShiftHeld(), WidgetId);

    /// <summary>[S-5542] A letter typed with the lease finds the next icon whose name starts so.</summary>
    public bool HandleText(char character) => Acts && _host.DesktopItems.Type(character);

    /// <summary>[S-6626] Going elsewhere keeps the icons selected, as the desktop keeps its icons selected.</summary>
    public bool KeepsSelectionWhenLeft => true;

    // ---- the pointer ([S-5540], [S-5549]) ------------------------------------------------------------

    /// <summary>A left press outside edit mode is the desktop's selection's; the card still gets the press (the lease, [S-800]).</summary>
    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!Acts || !point.Properties.IsLeftButtonPressed || _box is not null)
        {
            return;
        }

        var keys = e.KeyModifiers;
        var ctrl = keys.HasFlag(Windows.System.VirtualKeyModifiers.Control);
        var shift = keys.HasFlag(Windows.System.VirtualKeyModifiers.Shift);
        _host.DesktopItems.Press(Key, ctrl, shift);
        _pressed = true;
        _pressModified = ctrl || shift;
    }

    /// <summary>
    /// [S-11878] [S-5549] The card's move of the selection leaves the desktop (or reaches a drop target on
    /// it): the shell's drag of the selection starts here, inside the card's pointer handler, with that
    /// event's pointer, so XAML's drag takes over the press that is still down.
    /// </summary>
    public void DragOut(PointerPoint point)
    {
        _pressed = false;
        if (Acts)
        {
            _ = DragAsync(point);
        }
    }

    /// <summary>[S-11878] Whether a drop of files on this icon goes somewhere: its target's shell drop target, or an own icon waiting for its target.</summary>
    public bool TakesDrops => Acts && ((Own && _config.OpensNothing) || ForwardTarget() is not null);

    /// <summary>
    /// [S-5549] One drag, from the host's beginning (the selection rule, what it offers, the icons
    /// drawn as dragged) through XAML's drag to its end. Runs up to the drag's start without a pause, so
    /// that start is still inside the pointer handler; the host's state is cleared on every way out
    /// (dropped, cancelled with Escape, a start XAML refused, anything thrown).
    /// </summary>
    private async Task DragAsync(PointerPoint point)
    {
        var desk = _host.DesktopItems;
        ItemDragOffer? drag = null;
        try
        {
            drag = desk.BeginDrag(Key, Window());
            if (drag is null)
            {
                return;
            }

            var offer = drag;
            var effect = await ItemDrag.StartAsync(this, point, offer, () => desk.DragPicture(offer));
            Log.Debug(Scope, $"a drag of desktop icons ended: {effect}");
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a desktop icon's drag failed", ex);
        }
        finally
        {
            if (drag is not null)
            {
                desk.EndDrag(drag);
            }
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pressed)
        {
            _host.DesktopItems.Release(Key);
        }

        _pressed = false;
    }

    private void OnPressLost(object sender, PointerRoutedEventArgs e) => _pressed = false;

    /// <summary>[S-9505] A single click opens an own icon whose Open with says so; never a modified click, which only selects.</summary>
    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!Acts || !Own || _config.ClickMode != ItemClickMode.Single || _pressModified || _box is not null)
        {
            return;
        }

        e.Handled = true;
        _host.DesktopItems.Open(Key);
    }

    /// <summary>[S-5530] A double-click opens the selection, and is the icon's, never the card's.</summary>
    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_surface?.EditMode == true)
        {
            return;
        }

        e.Handled = true;
        if (Acts && _box is null && (!Own || _config.ClickMode == ItemClickMode.Double))
        {
            _host.DesktopItems.Open(Key);
        }
    }

    /// <summary>[S-5548] [S-5582] A right-click is the desktop's menu for the selection, never the card's menu.</summary>
    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_surface?.EditMode == true)
        {
            return;
        }

        e.Handled = true;
        if (!Acts || XamlRoot is not { } root)
        {
            return;
        }

        var scale = root.RasterizationScale > 0 ? root.RasterizationScale : 1.0;
        var at = e.GetPosition(null);
        var window = Window();
        var (x, y) = _host.ExplorerMenus.ToScreen(window, (int)Math.Round(at.X * scale), (int)Math.Round(at.Y * scale));
        // [S-5545] Shift held: the shell's classic menu at once, as Explorer.
        _host.DesktopItems.Context(Key, x, y, window, classic: SelectionInput.ShiftHeld());
    }

    /// <summary>The window this view is drawn in, for the menus and the drag's source surface.</summary>
    private nint Window()
        => XamlRoot?.ContentIslandEnvironment is { } environment
            ? Microsoft.UI.Win32Interop.GetWindowFromWindowId(environment.AppWindowId)
            : 0;

    // ---- drawing -----------------------------------------------------------------------------------

    /// <summary>
    /// [S-5602] Hover, selection (inactive without the keyboard), the focus after keyboard use and a
    /// drag's drop target, from the host's one desktop selection through the one selection chrome.
    /// [S-11934] [S-11945] The app or file pointed at elsewhere on the view, and the app picked there,
    /// look as the pointer would make them. [S-5545] [S-5547] A cut item and a hidden one have their
    /// icon faded, as Explorer draws them.
    /// </summary>
    private void Paint()
    {
        var key = Key;
        var desk = _host.DesktopItems;
        var selected = _fixture.On ? _fixture.Selected : desk.IsSelected(key);
        var focus = _fixture.On ? _fixture.Focused : desk.ShowsFocus(key);
        var active = _fixture.On ? !_fixture.Inactive : desk.Active;
        var dropTarget = _fixture.On ? _fixture.DropTarget : WidgetId is { } id && string.Equals(desk.DropTargetWidget, id, StringComparison.Ordinal);
        SelectionChrome.Paint(_chrome, new SelectionCue(selected, _hovered || LinkedElsewhere, focus, focus, active, dropTarget), SelectionLook.DesktopIcon);

        var faded = _fixture.On ? _fixture.Faded : !Own && (desk.IsCut(key) || Item?.Hidden == true);
        _iconHost.Opacity = faded ? SelectionLook.Faded : 1;

        // [S-5608] Told to a screen reader: the selection as it changes, the focus as it arrives.
        var focused = Acts && desk.IsFocused(key);
        if ((selected != _toldSelected || focused != _toldFocused)
            && Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(this) is DesktopItemAutomationPeer peer)
        {
            peer.Changed(_toldSelected, selected, _toldFocused, focused);
        }

        _toldSelected = selected;
        _toldFocused = focused;

        // [S-13974] Icon only's name shows while pointed at, focused or renamed.
        PaintName(focus);
    }

    private void Render()
    {
        // [S-5593] [S-13970] Another icon size or layout is in force: the icon's square and its name's place follow it, and
        // the icon is asked at its pixels below.
        if (_size != DesktopCell.Current || _layout != DesktopCell.CurrentLayout)
        {
            Relook(DesktopCell.Current, DesktopCell.CurrentLayout);
        }

        var fit = DesktopCell.Fit(_size, _layout, _config.LabelLines);
        _name.Text = ShownName;
        _name.MaxLines = fit.Lines;
        _name.TextMaxWidth = DesktopCell.NameWidth(_size, _layout, ActualWidth);
        BuildIcon();
        PaintHealth();
        SyncBox();
        Paint();
        _ = LoadIconAsync();
    }

    // ---- F2: the rename box ([S-5543], [S-5584], [S-5607]) --------------------------------------------

    /// <summary>The box shows while the host says this widget renames (or a preview's "editing" state), and goes when it no longer does.</summary>
    private void SyncBox()
    {
        var wanted = Renamable && (_fixture.On
            ? _fixture.Renaming
            : WidgetId is { } id && string.Equals(_host.DesktopItems.RenamingWidget, id, StringComparison.Ordinal));
        if (!wanted)
        {
            CloseBox();
            return;
        }

        if (_box is not null)
        {
            return;
        }

        // A file's name with the part before its extension selected; an own icon's name, all of it.
        var name = ShownName;
        var (text, select) = Item is { } item ? DesktopRename.Edit(item) : (name, name.Length);
        var box = new TextBox
        {
            Text = text,
            PlaceholderText = Own ? OwnIconRules.DerivedName(_config) : string.Empty,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            IsSpellCheckEnabled = false,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
            FontSize = FamilyType.Caption.FontSize,
        };

        // [S-13973] [S-13974] Where the name stands in the icon layout: under the icon, beside it, or past the cell.
        LayBox(box);
        _box = box;
        BoxHost.Children.Add(box);
        Paint();

        if (_fixture.On)
        {
            box.Loaded += (_, _) => box.Select(0, select);
            return;
        }

        box.GotFocus += (_, _) => _surface?.RequestTextInput(true);
        box.LostFocus += (_, _) =>
        {
            _surface?.RequestTextInput(false);
            if (!_closingBox)
            {
                Commit(box);
            }
        };
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                Commit(box);
            }
            else if (args.Key == Windows.System.VirtualKey.Escape)
            {
                // Escape reverts, and is the box's: the lease does not end with it.
                args.Handled = true;
                _host.DesktopItems.CancelRename();
            }
        };
        box.Loaded += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.Select(0, select);
        };
    }

    /// <summary>Enter or a click elsewhere: committed through the host; a name it refuses keeps the box and says why.</summary>
    private void Commit(TextBox box)
    {
        if (!ReferenceEquals(box, _box))
        {
            return;
        }

        if (_host.DesktopItems.CommitRename(Key, box.Text) is { } why)
        {
            var tip = new ToolTip { Content = why };
            ToolTipService.SetToolTip(box, tip);
            tip.IsOpen = true;
            box.Focus(FocusState.Programmatic);
        }
    }

    private void CloseBox()
    {
        if (_box is not { } box)
        {
            return;
        }

        _closingBox = true;
        try
        {
            _box = null;
            if (ToolTipService.GetToolTip(box) is ToolTip tip)
            {
                tip.IsOpen = false;
            }

            (box.Parent as Panel)?.Children.Remove(box);
            Paint();
            _surface?.RequestTextInput(false);
        }
        finally
        {
            _closingBox = false;
        }
    }

    /// <summary>Persist an edit the view made to its own config (a drop that set it up), and draw it.</summary>
    private void Save()
    {
        _surface?.RequestSave();
        Changed();
    }

    /// <summary>
    /// [S-5605] What a preview draws: a preview draws as given and acts on nothing, and its states draw
    /// the selection (active or not), the focus, a drop target, a cut item and the box. Its own item
    /// stands in for the host's listing; an own icon's preview draws a config of its own instead.
    /// </summary>
    private sealed class PreviewFixture
    {
        public bool On { get; set; }

        public bool Selected { get; set; }

        public bool Inactive { get; set; }

        public bool Focused { get; set; }

        public bool DropTarget { get; set; }

        public bool Faded { get; set; }

        public bool Renaming { get; set; }

        public Themia.Sdk.DesktopItem? Item { get; set; }

        /// <summary>Back to a plain icon at rest, for the next state.</summary>
        public void Rest(Themia.Sdk.DesktopItem? item)
        {
            On = true;
            Selected = false;
            Inactive = false;
            Focused = false;
            DropTarget = false;
            Faded = false;
            Renaming = false;
            Item = item;
        }
    }
}
