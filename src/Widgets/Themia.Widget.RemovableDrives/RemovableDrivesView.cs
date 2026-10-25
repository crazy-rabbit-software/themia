using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.RemovableDrives;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4660]..[S-4664] What is plugged in (Karpo §91): a row per USB stick, card, disc or external disk
/// with its picture, its name as This PC names it, what it holds, and Eject at the row's end — the
/// shell's own eject, which says how it went in Windows' own notice. Live: a device plugged in or out
/// shows at once (the host's walk, <see cref="IDrives"/>, runs again on <c>WM_DEVICECHANGE</c>). Nothing plugged
/// in says so, centred.
/// </summary>
internal sealed partial class RemovableDrivesView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Padding = WidgetLayout.ListInset,
    };

    private readonly StackPanel _list = new();
    private readonly List<DeviceRow> _rows = [];
    private readonly List<IDisposable> _hovers = [];
    private readonly StackPanel _empty;
    private ScrollFade? _fade;
    private IWidgetSurface? _surface;
    private IDisposable? _watch;
    private IReadOnlyList<DriveEntry> _devices = [];
#if DEBUG
    private IReadOnlyList<DriveEntry>? _previewDevices;
#else
    // Release has no renders: the field's Debug-only writer (the .Preview.cs part) is not compiled.
    private IReadOnlyList<DriveEntry>? _previewDevices => null;
#endif
    private WidgetArea _area;
    private bool _tall;
    private int _focus = -1;
    private bool _detached;

    /// <summary>[S-6913] What the view asks of the host: the launcher, ⚙ › Accounts.</summary>
    private readonly IWidgetHost _widgetHost;

    public RemovableDrivesView(IWidgetHost host)
    {
        _widgetHost = host;
        _scroll.Content = _list;
        Children.Add(_scroll);
        _fade = new ScrollFade(_scroll, _list);

        _empty = Fluent.EmptyState(Glyphs.Usb, Fluent.Text(FluentType.Body), out _);
        _empty.Visibility = Visibility.Collapsed;
        Children.Add(_empty);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _watch?.Dispose();
        _watch = _widgetHost.Drives.Watch(ticker, Show);
        Show();
    }

    public void ApplyConfig(WidgetConfig config) => Show();

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void OnLanguage(Locale locale) => Lay();

    private void Show()
    {
        if (_previewDevices is null && !_widgetHost.Drives.HasValue)
        {
            return;
        }

        _devices = _previewDevices ?? DriveRules.Removables(_widgetHost.Drives.Drives);
        Lay();
    }

    /// <summary>[S-4662] A short tile one line a device; from Regular height two, with the bar.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var area = _area.IsMeasured ? _area : new WidgetArea(ActualWidth, ActualHeight);
        _tall = area.HeightClass >= SizeClass.Regular && area.WidthClass >= SizeClass.Regular;
        var wide = area.Width >= 260;

        var none = _devices.Count == 0;
        _empty.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
        _scroll.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
        ((TextBlock)_empty.Children[1]).Text = RemovableDrivesStr.Drives_Nothing.Text();

        while (_rows.Count < _devices.Count)
        {
            var row = new DeviceRow(this);
            _hovers.Add(Fluent.Hover(row.Root));
            _rows.Add(row);
            _list.Children.Add(row.Root);
        }

        var locale = Strings.Current;
        for (var i = 0; i < _rows.Count; i++)
        {
            var shown = i < _devices.Count;
            _rows[i].Root.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            if (shown)
            {
                _rows[i].Show(_devices[i], _tall, wide, area.Width >= 160, locale);
            }
        }

        if (_focus >= _devices.Count)
        {
            Focus(ListKeys.AfterRemoval(_focus, _devices.Count));
        }

        _fade?.Refresh();
    }

    private void Open(DriveEntry device) => _widgetHost.Launcher.Open(device.Root);

    private void Eject(DriveEntry device)
    {
        if (_surface?.IsPreview ?? false)
        {
            return;
        }

        _widgetHost.Launcher.Eject(device.Root);
    }

    // ---- keys ([S-4664]) ----------------------------------------------------------------------

    public bool WantsKeys => !_detached && _devices.Count > 0;

    /// <summary>Up/Down walk the devices, Enter opens one, Delete ejects it, Escape lets go.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var decision = ListKeys.Decide(key, _focus, _devices.Count);

        switch (decision.Command)
        {
            case ListCommand.Focus:
                Focus(decision.Index);
                return true;
            case ListCommand.Open when decision.Index >= 0 && decision.Index < _devices.Count:
                Open(_devices[decision.Index]);
                return true;
            case ListCommand.Delete when decision.Index >= 0 && decision.Index < _devices.Count:
                Eject(_devices[decision.Index]);
                return true;
            case ListCommand.Release:
                var had = _focus >= 0;
                Focus(-1);
                return had;
            default:
                return false;
        }
    }

    /// <summary>E ejects the focused device.</summary>
    public bool HandleText(char character)
    {
        if (_detached || char.ToUpperInvariant(character) != 'E' || _focus < 0 || _focus >= _devices.Count)
        {
            return false;
        }

        Eject(_devices[_focus]);
        return true;
    }

    private void Focus(int index)
    {
        _focus = index;
        for (var i = 0; i < _rows.Count; i++)
        {
            RowFocus.Mark(_rows[i].Root, i == index);
        }
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        _watch?.Dispose();
        _watch = null;
        _fade?.Dispose();
        _fade = null;

        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();
    }

    /// <summary>One device: picture, name and what it holds, a bar under them on a tall tile, Eject at the end.</summary>
    private sealed class DeviceRow
    {
        private readonly RemovableDrivesView _owner;
        private readonly DriveIcon _icon = new();
        private readonly TextBlock _name = Fluent.Text(FluentType.Body);
        private readonly TextBlock _space = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        private readonly ProgressBar _bar = DriveBar.Create();
        private readonly Button _eject = new();
        private readonly Grid _grid;
        private DriveEntry? _device;

        public DeviceRow(RemovableDrivesView owner)
        {
            _owner = owner;
            _name.TextTrimming = TextTrimming.CharacterEllipsis;
            _space.TextTrimming = TextTrimming.CharacterEllipsis;

            _grid = new Grid { ColumnSpacing = Fluent.Space3 };
            var grid = _grid;
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRowSpan(_icon, 3);
            _icon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_name, 1);
            Grid.SetColumn(_space, 1);
            Grid.SetRow(_space, 1);
            Grid.SetColumn(_bar, 1);
            Grid.SetRow(_bar, 2);
            Grid.SetColumn(_eject, 2);
            Grid.SetRowSpan(_eject, 3);
            _eject.VerticalAlignment = VerticalAlignment.Center;
            _bar.Margin = new Thickness(0, 2, 0, 0);

            _eject.Click += (_, _) =>
            {
                if (_device is { } device)
                {
                    _owner.Eject(device);
                }
            };

            grid.Children.Add(_icon);
            grid.Children.Add(_name);
            grid.Children.Add(_space);
            grid.Children.Add(_bar);
            grid.Children.Add(_eject);
            grid.VerticalAlignment = VerticalAlignment.Center;

            Root = Fluent.Row(grid);
            RowFocus.Prepare(Root);
            Root.PointerPressed += OnPressed;
            Root.DoubleTapped += (_, _) =>
            {
                if (_device is { } device)
                {
                    _owner.Open(device);
                }
            };
        }

        public Border Root { get; }

        private void OnPressed(object sender, PointerRoutedEventArgs args)
        {
            var index = _owner._rows.IndexOf(this);
            if (index >= 0)
            {
                _owner.Focus(index);
            }
        }

        public void Show(DriveEntry device, bool tall, bool wide, bool roomy, Locale locale)
        {
            _device = device;
            var name = DriveRules.Title(locale, device);
            _name.Text = name;
            _space.Text = DriveRules.SpaceLine(locale, device);
            _icon.Show(device, tall ? 32 : Fluent.IconSize + 4);

            // [S-4662] One line: the name alone (what it holds is the tooltip); two: the space and the bar under it.
            _space.Visibility = tall ? Visibility.Visible : Visibility.Collapsed;
            DriveBar.Show(_bar, device, 3);
            if (!tall)
            {
                _bar.Visibility = Visibility.Collapsed;
            }

            Root.MinHeight = tall ? 64 : Fluent.RowHeight;

            // Collapsed rows still take the grid's spacing: one line has none, so the name centres.
            _grid.RowSpacing = tall ? 2 : 0;
            Grid.SetRowSpan(_name, tall ? 1 : 3);
            _name.VerticalAlignment = VerticalAlignment.Center;
            _bar.Margin = tall ? new Thickness(0, 2, 0, 0) : new Thickness(0);

            // A tile too narrow for picture, name and button gives up the picture.
            _icon.Visibility = roomy ? Visibility.Visible : Visibility.Collapsed;

            // "Eject", as Explorer says it; the stick's glyph where the word does not fit.
            _eject.Content = wide ? Str.Drives_Eject.Text() : new FontIcon { Glyph = Glyphs.Usb, FontSize = Fluent.IconSize };
            _eject.Padding = wide ? new Thickness(Fluent.Space3, Fluent.Space1, Fluent.Space3, Fluent.Space1) : new Thickness(Fluent.Space2);
            ToolTipService.SetToolTip(_eject, RemovableDrivesStr.Drives_EjectNamed.Format(("name", name)));
            ToolTipService.SetToolTip(Root, $"{name} · {DriveRules.SpaceLine(locale, device)}");
        }
    }
}
