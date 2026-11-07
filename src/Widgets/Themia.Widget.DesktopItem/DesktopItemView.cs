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
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5600]..[S-5607] One desktop item as Explorer's desktop draws an icon: the shell's icon with its
/// overlay over its name (legible over any wallpaper, [S-6733]), in the one selection look ([S-6627]).
/// Its gestures are the desktop's ([S-5540]..[S-5549]): a press, a key, a typed letter, the right
/// button, a drag and a drop are handed to the host's one desktop selection
/// (<see cref="IWidgetHost.DesktopItems"/>), which paints back what it decided; F2's box is drawn here.
/// A right-click never reaches the card, which would open its menu.
/// </summary>
internal sealed partial class DesktopItemView : Grid, IWidgetView, IWidgetSizeAware, IWidgetKeys, IWidgetSurfaceAware, IWidgetDropTarget
{
    private const string Scope = "desktop-item";

    private readonly IWidgetHost _host;
    private readonly Image _icon = new() { Stretch = Stretch.Uniform };
    private readonly WallpaperLabel _name = new(FamilyType.Caption);
    private readonly StackPanel _stack = new() { Spacing = DesktopItemLayout.Gap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
    private readonly Border _chrome;
    private IDisposable? _hover;
    private IWidgetSurface? _surface;
    private DesktopItemConfig? _config;

    /// <summary>[S-5605] What a debug render's preview state sets (never set on the desktop).</summary>
    private readonly PreviewFixture _fixture = new();
    private bool _hovered;
    private ItemFit _fit = DesktopItemLayout.For(DesktopItemModule.CellWidth, DesktopItemModule.CellHeight);
    private int _iconRequest;

    /// <summary>[S-5549] A left press waiting to become a drag (or a release), and where it was.</summary>
    private bool _pressed;
    private Point _pressAt;

    /// <summary>[S-5543] The rename box while it shows, and whether it is being taken down (its LostFocus then commits nothing).</summary>
    private TextBox? _box;
    private bool _closingBox;

    public DesktopItemView(IWidgetHost host)
    {
        _host = host;
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _stack.Children.Add(_icon);
        _stack.Children.Add(_name);

        _chrome = SelectionChrome.Wrap(_stack);
        _chrome.Padding = new Thickness(DesktopItemLayout.Inset);
        _chrome.Margin = new Thickness(0);
        Children.Add(_chrome);

        // Clear, not null: the empty part of the cell takes the press too.
        Background = Fluent.Clear();
    }

    /// <summary>The item drawn: the preview's own, else the host's listing's, else one named from the key alone.</summary>
    private Themia.Sdk.DesktopItem? Item
    {
        get
        {
            if (_fixture.Item is not null)
            {
                return _fixture.Item;
            }

            if (_config is not { Item.Length: > 0 } config)
            {
                return null;
            }

            return _host.DesktopItems.Find(config.Item)
                ?? new Themia.Sdk.DesktopItem(config.Item, DesktopItemKeys.FallbackName(config.Item), DesktopItemKind.File);
        }
    }

    /// <summary>Whether gestures act: on the desktop, outside edit mode, never in a preview.</summary>
    internal bool Acts => _surface?.EditMode != true && !_fixture.On && !_host.IsPreview;

    /// <summary>[S-5570] The host, for the item's automation peer.</summary>
    internal IWidgetHost Host => _host;

    /// <summary>[S-5570] The item drawn, for the item's automation peer.</summary>
    internal Themia.Sdk.DesktopItem? ShownItem => Item;

    /// <summary>[S-5570] What was last announced: a change of either is told to a screen reader.</summary>
    private bool _toldSelected;
    private bool _toldFocused;

    /// <summary>[S-5570] A screen reader sees the item as a list item of the desktop's list.</summary>
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new DesktopItemAutomationPeer(this);

    private string? WidgetId => _surface?.WidgetId;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Detach();
        _host.DesktopItems.Changed += OnItemsChanged;
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += OnPressLost;
        PointerCanceled += OnPressLost;
        DoubleTapped += OnDoubleTapped;
        RightTapped += OnRightTapped;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _hover = SelectionChrome.Hover(this, hovered =>
        {
            _hovered = hovered;
            Paint();
        });
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as DesktopItemConfig;
        Render();
    }

    /// <summary>[S-5601] The ladder is measured on what the view is really given (the cell less the design's gap), on its size change.</summary>
    public void ApplySize(WidgetArea area) => Fit(ActualWidth > 0 ? ActualWidth : area.Width, ActualHeight > 0 ? ActualHeight : area.Height);

    private void Fit(double width, double height)
    {
        var fit = DesktopItemLayout.For(width, height);
        if (fit != _fit)
        {
            _fit = fit;
            Render();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Fit(e.NewSize.Width, e.NewSize.Height);

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Detach()
    {
        _host.DesktopItems.Changed -= OnItemsChanged;
        PointerPressed -= OnPressed;
        PointerMoved -= OnMoved;
        PointerReleased -= OnReleased;
        PointerCaptureLost -= OnPressLost;
        PointerCanceled -= OnPressLost;
        DoubleTapped -= OnDoubleTapped;
        RightTapped -= OnRightTapped;
        SizeChanged -= OnSizeChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        WatchScale(null);
        _hover?.Dispose();
        _hover = null;
        _pressed = false;
        CloseBox();
    }

    /// <summary>
    /// [S-5541] [S-5545] The keys with the lease are the desktop's (the arrows by place, Enter, F2, Delete,
    /// the clipboard, the Menu key); Escape is the lease's own end ([S-813]), which keeps the selection.
    /// </summary>
    public bool HandleKey(WidgetKey key)
        => key != WidgetKey.Escape && Acts && _host.DesktopItems.Key(key, SelectionInput.ShiftHeld(), WidgetId);

    /// <summary>[S-5542] A letter typed with the lease finds the next item whose name starts so.</summary>
    public bool HandleText(char character) => Acts && _host.DesktopItems.Type(character);

    /// <summary>[S-6626] Going elsewhere keeps the items selected, as the desktop keeps its icons selected.</summary>
    public bool KeepsSelectionWhenLeft => true;

    private void OnItemsChanged() => Render();

    /// <summary>[S-5563] The root the view is drawn in, watched for its scale (a display's scale changed).</summary>
    private XamlRoot? _scaleRoot;

    private void OnLoaded(object sender, RoutedEventArgs e) => WatchScale(XamlRoot);

    private void OnUnloaded(object sender, RoutedEventArgs e) => WatchScale(null);

    private void WatchScale(XamlRoot? root)
    {
        if (ReferenceEquals(root, _scaleRoot))
        {
            return;
        }

        if (_scaleRoot is not null)
        {
            _scaleRoot.Changed -= OnRootChanged;
        }

        _scaleRoot = root;
        if (root is not null)
        {
            root.Changed += OnRootChanged;
        }
    }

    /// <summary>[S-5563] A new scale wants the icon at its own pixels, never the old one's stretched.</summary>
    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (DesktopIconPixels.For(_fit.Icon, sender.RasterizationScale) != _iconPixels)
        {
            LoadIcon(Item);
        }
    }

    /// <summary>[S-5540] A left press outside edit mode is the desktop's selection's; the card still gets the press (the lease, [S-800]).</summary>
    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!Acts || Item is not { } item || !point.Properties.IsLeftButtonPressed || _box is not null)
        {
            return;
        }

        var keys = e.KeyModifiers;
        _host.DesktopItems.Press(item.Key, keys.HasFlag(Windows.System.VirtualKeyModifiers.Control), keys.HasFlag(Windows.System.VirtualKeyModifiers.Shift));
        _pressed = true;
        _pressAt = point.Position;
    }

    /// <summary>[S-5549] Past the dead zone with the button held, the press is a drag of the selection: posted, so OLE's loop never nests in XAML's input.</summary>
    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed || Item is not { } item)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _pressed = false;
            return;
        }

        if (!SelectionBand.PastThreshold(point.Position.X - _pressAt.X, point.Position.Y - _pressAt.Y))
        {
            return;
        }

        _pressed = false;
        var key = item.Key;
        var window = Window();
        if (DispatcherQueue is not { } queue || !queue.TryEnqueue(() => _host.DesktopItems.DragOut(key, window)))
        {
            Log.Warn(Scope, "a desktop item's drag could not be started");
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pressed && Item is { } item)
        {
            _host.DesktopItems.Release(item.Key);
        }

        _pressed = false;
    }

    private void OnPressLost(object sender, PointerRoutedEventArgs e) => _pressed = false;

    /// <summary>[S-5530] A double-click opens the selection, and is the item's.</summary>
    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_surface?.EditMode == true)
        {
            return;
        }

        e.Handled = true;
        if (Acts && Item is { } item)
        {
            _host.DesktopItems.Open(item.Key);
        }
    }

    /// <summary>[S-5548] A right-click is the shell's menu for the selection, never the card's menu.</summary>
    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_surface?.EditMode == true)
        {
            return;
        }

        e.Handled = true;
        if (!Acts || Item is not { } item || XamlRoot is not { } root)
        {
            return;
        }

        var scale = root.RasterizationScale > 0 ? root.RasterizationScale : 1.0;
        var at = e.GetPosition(null);
        var window = Window();
        var (x, y) = _host.ExplorerMenus.ToScreen(window, (int)Math.Round(at.X * scale), (int)Math.Round(at.Y * scale));
        _host.DesktopItems.Context(item.Key, x, y, window);
    }

    /// <summary>The window this view is drawn in, for the shell's menus and OLE.</summary>
    private nint Window()
        => XamlRoot?.ContentIslandEnvironment is { } environment
            ? Microsoft.UI.Win32Interop.GetWindowFromWindowId(environment.AppWindowId)
            : 0;

    // ---- drops onto the item ([S-5549]) -------------------------------------------------------------

    DropEffect IWidgetDropTarget.DragOver(IWidgetDrag drag, Point local)
    {
        if (!Acts || Item is not { } item || WidgetId is not { } id || _host.DesktopItems.Dragging(item.Key))
        {
            ClearDropTarget();
            return DropEffect.None;
        }

        drag.Place = item.Name;
        var effect = drag.ForwardOver(item.Key, DropEffect.None);
        _host.DesktopItems.SetDropTarget(effect == DropEffect.None ? null : id);
        return effect;
    }

    void IWidgetDropTarget.DragLeave() => ClearDropTarget();

    DropEffect IWidgetDropTarget.Drop(IWidgetDrag drag, Point local)
    {
        ClearDropTarget();
        return Acts && Item is { } item && !_host.DesktopItems.Dragging(item.Key)
            ? drag.ForwardDrop(item.Key, DropEffect.None)
            : DropEffect.None;
    }

    private void ClearDropTarget()
    {
        if (WidgetId is { } id && string.Equals(_host.DesktopItems.DropTargetWidget, id, StringComparison.Ordinal))
        {
            _host.DesktopItems.SetDropTarget(null);
        }
    }

    // ---- drawing -----------------------------------------------------------------------------------

    private bool Selected => _fixture.On ? _fixture.Selected : Item is { } item && _host.DesktopItems.IsSelected(item.Key);

    private void Paint()
    {
        var key = Item?.Key;
        var focus = _fixture.On ? _fixture.Focused : key is not null && _host.DesktopItems.ShowsFocus(key);
        var active = _fixture.On || _host.DesktopItems.Active;
        var dropTarget = _fixture.On ? _fixture.DropTarget : WidgetId is { } id && string.Equals(_host.DesktopItems.DropTargetWidget, id, StringComparison.Ordinal);
        SelectionChrome.Paint(_chrome, new SelectionCue(Selected, _hovered, focus, focus, active, dropTarget));

        // [S-5545] [S-5547] A cut item and a hidden one are faded, as Explorer draws them.
        var faded = _fixture.On ? _fixture.Faded : key is not null && (_host.DesktopItems.IsCut(key) || Item?.Hidden == true);
        _icon.Opacity = faded ? SelectionLook.Faded : 1;

        // [S-5570] Told to a screen reader: the selection as it changes, the focus as it arrives.
        var selected = Selected;
        var focused = Acts && key is not null && _host.DesktopItems.IsFocused(key);
        if ((selected != _toldSelected || focused != _toldFocused)
            && Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(this) is DesktopItemAutomationPeer peer)
        {
            peer.Changed(_toldSelected, selected, _toldFocused, focused);
        }

        _toldSelected = selected;
        _toldFocused = focused;
    }

    private void Render()
    {
        var item = Item;
        _name.Text = item?.Name ?? string.Empty;
        _name.MaxLines = Math.Max(_fit.Lines, 1);
        _name.TextMaxWidth = NameWidth;
        _icon.Width = _fit.Icon;
        _icon.Height = _fit.Icon;
        Paint();
        SyncBox(item);
        _name.Visibility = _fit.Lines > 0 && _box is null ? Visibility.Visible : Visibility.Collapsed;
        LoadIcon(item);
    }

    private double NameWidth => Math.Max(DesktopItemLayout.MinNameWidth, ActualWidth > 0 ? ActualWidth - (2 * DesktopItemLayout.Inset) : DesktopItemModule.CellWidth - (2 * DesktopItemLayout.Inset));

    // ---- F2: the rename box ([S-5543]) ----------------------------------------------------------------

    /// <summary>The box shows while the host says this widget renames (or a preview's "renaming" state), and goes when it no longer does.</summary>
    private void SyncBox(Themia.Sdk.DesktopItem? item)
    {
        var wanted = item is not null && (_fixture.On
            ? _fixture.Renaming
            : WidgetId is { } id && string.Equals(_host.DesktopItems.RenamingWidget, id, StringComparison.Ordinal));
        if (!wanted)
        {
            CloseBox();
            return;
        }

        if (_box is not null || item is null)
        {
            return;
        }

        var (text, select) = DesktopRename.Edit(item);
        var box = new TextBox
        {
            Text = text,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
            // Out into the cell's inset: Explorer's box is wider than the name it edits.
            Margin = new Thickness(-DesktopItemLayout.Inset, 0, -DesktopItemLayout.Inset, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = FamilyType.Caption.FontSize,
        };
        _box = box;
        _stack.Children.Add(box);

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
        if (Item is not { } item || !ReferenceEquals(box, _box))
        {
            return;
        }

        if (_host.DesktopItems.CommitRename(item.Key, box.Text) is { } why)
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

            _stack.Children.Remove(box);
        }
        finally
        {
            _closingBox = false;
        }
    }

    /// <summary>[S-5563] The pixels the icon was last asked at.</summary>
    private int _iconPixels;

    private async void LoadIcon(Themia.Sdk.DesktopItem? item)
    {
        var request = ++_iconRequest;
        if (item is null)
        {
            _icon.Source = null;
            return;
        }

        // [S-5563] The icon at this display's own scale: 48 pixels at 100 %, 72 at 150 %, 96 at 200 %.
        var pixels = DesktopIconPixels.For(_fit.Icon, XamlRoot?.RasterizationScale ?? 1.0);
        _iconPixels = pixels;
        if (_host.DesktopItems.TryIcon(item, pixels, out var cached))
        {
            _icon.Source = cached;
            return;
        }

        try
        {
            var image = await _host.DesktopItems.IconAsync(item, pixels).ConfigureAwait(true);
            if (request == _iconRequest)
            {
                _icon.Source = image;
            }
        }
        catch (Exception ex)
        {
            // An async void: nothing may escape.
            Log.Error(Scope, "loading a desktop item's icon threw", ex);
        }
    }

    /// <summary>
    /// [S-5605] A preview's own item, standing in for the host's listing; a preview draws as given and
    /// acts on nothing, and its states draw the selection, the focus, a drop target, a cut item and the box.
    /// </summary>
    private sealed class PreviewFixture
    {
        public bool On { get; set; }

        public bool Selected { get; set; }

        public bool Focused { get; set; }

        public bool DropTarget { get; set; }

        public bool Faded { get; set; }

        public bool Renaming { get; set; }

        public Themia.Sdk.DesktopItem? Item { get; set; }
    }
}
