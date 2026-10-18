using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4860] How full the Recycle Bin is: its size, its item count and an Empty button that hands the
/// emptying to Windows, confirmation and all. A click elsewhere on the tile opens the bin. The
/// answer is the host's one answer (<see cref="IWidgetHost.RecycleBin"/>) the Shortcuts badge reads, on its cadence:
/// one query for every widget showing the bin.
/// </summary>
internal sealed partial class RecycleBinView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetContextActions
{
    private readonly FontIcon _icon = Fluent.Icon(Glyphs.RecycleBin, "TextFillColorSecondaryBrush", RecycleBinLayout.LargeGlyph);
    private readonly TextBlock _size = Fluent.Text(FluentType.Strong(RecycleBinLayout.SizeStepFor(false)));
    private readonly TextBlock _count = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly TextBlock _buttonLabel = new();
    private readonly Button _empty;
    private readonly StackPanel _text = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _content = new() { Margin = WidgetLayout.Inset, VerticalAlignment = VerticalAlignment.Center };

    private IWidgetSurface? _surface;
    private IDisposable? _binWatch;
    private RecycleBinPlan _plan = RecycleBinLayout.Plan(default);
    private bool _detached;

    /// <summary>The answer drawn: the shared one, which a preview replaces.</summary>
    private Func<RecycleBinState?> _state;

    /// <summary>[S-6913] What the view asks of the host: the launcher, the Recycle Bin.</summary>
    private readonly IWidgetHost _widgetHost;

    public RecycleBinView(IWidgetHost host)
    {
        _widgetHost = host;
        _state = () => host.RecycleBin.Current;
        _size.TextTrimming = TextTrimming.CharacterEllipsis;
        _count.TextTrimming = TextTrimming.CharacterEllipsis;
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_size, FontNumeralAlignment.Tabular);
        _text.Children.Add(_size);
        _text.Children.Add(_count);

        _empty = new Button
        {
            Content = _buttonLabel,
            MinHeight = RecycleBinLayout.ButtonHeight,
            MinWidth = RecycleBinLayout.ButtonWidth,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _empty.Click += OnEmptyClicked;

        _content.Children.Add(_icon);
        _content.Children.Add(_text);
        _content.Children.Add(_empty);
        Children.Add(_content);

        // The whole tile is the way into the bin, as its icon on the desktop is.
        Background = ThemeBrush.Of("SubtleFillColorTransparentBrush", Microsoft.UI.Colors.Transparent);
        Tapped += OnTapped;
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        Strings.Changed += OnLanguage;
        _widgetHost.RecycleBin.Changed += Render;
        _binWatch ??= _widgetHost.RecycleBin.Watch();
        Render();
    }

    public void ApplyConfig(WidgetConfig config) => Render();

    public void ApplySize(WidgetArea area)
    {
        _plan = RecycleBinLayout.Plan(area.Deflate(WidgetLayout.ContentInset, WidgetLayout.ContentInset));
        Lay();
        Render();
    }

    /// <summary>[S-4863] Open the bin, and Empty — the same command as the button, for a tile too small for it.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
    [
        new(Glyphs.Open, Str.RecycleBin_Open.Text(), OpenBin),
        new(Glyphs.RecycleBin, Str.RecycleBin_EmptyEllipsis.Text(), () => _ = EmptyAsync()),
    ];

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _widgetHost.RecycleBin.Changed -= Render;
        _binWatch?.Dispose();
        _binWatch = null;
        _empty.Click -= OnEmptyClicked;
        Tapped -= OnTapped;
    }

    private void OnLanguage(Locale locale) => Render();

    /// <summary>[S-4862] Place the parts: stacked and centred, or icon, text and button across a strip.</summary>
    private void Lay()
    {
        _content.ColumnDefinitions.Clear();
        _content.RowDefinitions.Clear();
        _icon.Visibility = _plan.Glyph > 0 ? Visibility.Visible : Visibility.Collapsed;
        _icon.FontSize = _plan.Glyph > 0 ? _plan.Glyph : RecycleBinLayout.LargeGlyph;
        _size.Apply(FluentType.Strong(RecycleBinLayout.SizeStepFor(_plan.Large)));
        _count.Visibility = _plan.Count ? Visibility.Visible : Visibility.Collapsed;
        _empty.Visibility = _plan.Button ? Visibility.Visible : Visibility.Collapsed;

        if (_plan.Side)
        {
            _content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Place(_icon, 0, 0, new Thickness(0, 0, Fluent.Space3, 0), HorizontalAlignment.Left);
            Place(_text, 0, 1, new Thickness(0), HorizontalAlignment.Stretch);
            Place(_empty, 0, 2, new Thickness(Fluent.Space2, 0, 0, 0), HorizontalAlignment.Right);
            _size.HorizontalAlignment = HorizontalAlignment.Left;
            _count.HorizontalAlignment = HorizontalAlignment.Left;
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        Place(_icon, 0, 0, new Thickness(0, 0, 0, RecycleBinLayout.Gap), HorizontalAlignment.Center);
        Place(_text, 1, 0, new Thickness(0), HorizontalAlignment.Center);
        Place(_empty, 2, 0, new Thickness(0, RecycleBinLayout.Gap, 0, 0), HorizontalAlignment.Center);
        _size.HorizontalAlignment = HorizontalAlignment.Center;
        _count.HorizontalAlignment = HorizontalAlignment.Center;
    }

    private static void Place(FrameworkElement element, int row, int column, Thickness margin, HorizontalAlignment alignment)
    {
        SetRow(element, row);
        SetColumn(element, column);
        element.Margin = margin;
        element.HorizontalAlignment = alignment;
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var locale = Strings.Current;
        var state = _state();
        _size.Text = RecycleBinLayout.SizeText(locale, state);
        _count.Text = RecycleBinLayout.CountText(locale, state) ?? string.Empty;
        _buttonLabel.Text = Str.RecycleBin_Empty.Text();

        // Empty is offered only when there is something to empty, and not twice at once (process-wide).
        _empty.IsEnabled = RecycleBinState.CanEmpty(state, _widgetHost.RecycleBin.Emptying);
        ToolTipService.SetToolTip(this, RecycleBinState.Tooltip(locale, Str.WidgetsC_Location_RecycleBin.Text(), state));
    }

    private void OnEmptyClicked(object sender, RoutedEventArgs args) => _ = EmptyAsync();

    /// <summary>[S-4863] Windows asks; the widget never empties on its own say. The shared status guards and redraws.</summary>
    private Task EmptyAsync()
        => _detached || _surface?.IsPreview == true ? Task.CompletedTask : _widgetHost.RecycleBin.EmptyAsync();

    private void OnTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_surface?.EditMode == true || args.OriginalSource is DependencyObject source && Within(source, _empty))
        {
            return;
        }

        OpenBin();
    }

    private void OpenBin()
    {
        if (_surface?.IsPreview != true)
        {
            _widgetHost.Launcher.Open(RecycleBinState.Target);
        }
    }

    private static bool Within(DependencyObject source, DependencyObject ancestor)
    {
        for (var node = source; node is not null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
