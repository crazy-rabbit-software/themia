using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5600]..[S-5605] One desktop item as Explorer's desktop draws an icon: the shell's icon with its
/// overlay over its name, in the one selection look ([S-6627]). A press selects it alone across
/// every display ([S-5530]); a double-click or Enter opens it through the launcher, and the
/// double-click never reaches the card, which would toggle edit mode.
/// </summary>
internal sealed partial class DesktopItemView : Grid, IWidgetView, IWidgetSizeAware, IWidgetKeys, IWidgetSurfaceAware
{
    private const string Scope = "desktop-item";

    private readonly IWidgetHost _host;
    private readonly Image _icon = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _name = Family.Text(FamilyType.Caption, ColourRole.TextPrimary);
    private readonly StackPanel _stack = new() { Spacing = DesktopItemLayout.Gap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
    private readonly Border _chrome;
    private IDisposable? _hover;
    private IWidgetSurface? _surface;
    private DesktopItemConfig? _config;

    /// <summary>[S-5605] A preview's own item, standing in for the host's listing (a debug render).</summary>
    private Themia.Sdk.DesktopItem? _shown;

    /// <summary>[S-5605] A preview draws as given and acts on nothing; its "selected" state draws the selection.</summary>
    private bool _preview;

    private bool _previewSelected;
    private bool _hovered;
    private ItemFit _fit = DesktopItemLayout.For(DesktopItemModule.CellWidth, DesktopItemModule.CellHeight);
    private int _iconRequest;

    public DesktopItemView(IWidgetHost host)
    {
        _host = host;
        _name.TextAlignment = TextAlignment.Center;
        _name.TextWrapping = TextWrapping.WrapWholeWords;
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
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
            if (_shown is not null)
            {
                return _shown;
            }

            if (_config is not { Item.Length: > 0 } config)
            {
                return null;
            }

            return _host.DesktopItems.Find(config.Item)
                ?? new Themia.Sdk.DesktopItem(config.Item, DesktopItemKeys.FallbackName(config.Item), DesktopItemKind.File);
        }
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _host.DesktopItems.Changed -= OnItemsChanged;
        _host.DesktopItems.Changed += OnItemsChanged;
        PointerPressed -= OnPressed;
        PointerPressed += OnPressed;
        DoubleTapped -= OnDoubleTapped;
        DoubleTapped += OnDoubleTapped;
        SizeChanged -= OnSizeChanged;
        SizeChanged += OnSizeChanged;
        _hover?.Dispose();
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
        DoubleTapped -= OnDoubleTapped;
        SizeChanged -= OnSizeChanged;
        _hover?.Dispose();
        _hover = null;
    }

    /// <summary>[S-5530] Enter opens; Escape (the lease ending, [S-813]) keeps the selection, as the desktop does.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (key == WidgetKey.Enter)
        {
            Open();
            return true;
        }

        return false;
    }

    /// <summary>[S-6626] Going elsewhere keeps the item selected, as the desktop keeps its icons selected.</summary>
    public bool KeepsSelectionWhenLeft => true;

    private void OnItemsChanged() => Render();

    /// <summary>[S-5530] A left press outside edit mode selects this item alone; the card still gets the press (the lease, [S-800]).</summary>
    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_surface?.EditMode == true || Item is not { } item || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_preview)
        {
            return;
        }

        _host.DesktopItems.Select(item.Key);
    }

    /// <summary>[S-5530] A double-click opens the item, and is the item's: the card does not toggle edit mode.</summary>
    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_surface?.EditMode == true)
        {
            return;
        }

        e.Handled = true;
        Open();
    }

    private void Open()
    {
        if (_preview || _host.IsPreview || Item is not { } item)
        {
            return;
        }

        var outcome = _host.Launcher.Open(DesktopItemKeys.LaunchTarget(item.Key));
        if (outcome != LaunchOutcome.Opened)
        {
            Log.Warn(Scope, $"a desktop item did not open ({outcome})");
        }
    }

    private bool Selected => Item is { } item && string.Equals(_host.DesktopItems.Selected, item.Key, StringComparison.OrdinalIgnoreCase);

    private void Paint() => SelectionChrome.Paint(_chrome, new SelectionCue(Selected || (_preview && _previewSelected), _hovered));

    private void Render()
    {
        var item = Item;
        _name.Text = item?.Name ?? string.Empty;
        _name.MaxLines = Math.Max(_fit.Lines, 1);
        _name.Visibility = _fit.Lines > 0 ? Visibility.Visible : Visibility.Collapsed;
        _name.MaxWidth = Math.Max(DesktopItemLayout.MinNameWidth, ActualWidth > 0 ? ActualWidth - (2 * DesktopItemLayout.Inset) : DesktopItemModule.CellWidth - (2 * DesktopItemLayout.Inset));
        _icon.Width = _fit.Icon;
        _icon.Height = _fit.Icon;
        Paint();
        LoadIcon(item);
    }

    private async void LoadIcon(Themia.Sdk.DesktopItem? item)
    {
        var request = ++_iconRequest;
        if (item is null)
        {
            _icon.Source = null;
            return;
        }

        var scale = XamlRoot?.RasterizationScale is > 0 and var s ? s : 1.0;
        var pixels = (int)Math.Round(_fit.Icon * scale);
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
}
