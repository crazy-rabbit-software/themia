using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.RecentFiles;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4840] The files opened last, as Windows lists them in Recent Items: icon, name, folder and
/// when, newest first. A click opens the file; the row menu opens its location or takes it off the
/// list. The list is the shared <see cref="RecentItems"/>, redrawn when its folder changes; the
/// minute tick only keeps "2h" honest. Keys through the lease, as every list ([S-1288]).
/// </summary>
internal sealed partial class RecentFilesView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    /// <summary>The time column's width, so the times line up.</summary>
    private const double TimeWidth = 40;

    private readonly StackPanel _list = new() { Spacing = 0 };
    private readonly ScrollViewer _scroll;
    private readonly ScrollFade _fade;
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();
    private readonly List<Border> _rows = [];
    private readonly List<IDisposable> _rowHandles = [];

    private IWidgetSurface? _surface;
    private IDisposable? _items;
    private IDisposable? _tick;
    private IReadOnlyList<RecentFile> _files = [];
    private bool _unreadable;
    private RecentFilesPlan _plan = RecentFilesLayout.Plan(default);
    private int _cursor = -1;
    private bool _detached;
    private double _scale = 1;

    /// <summary>[S-6913] What the view asks of the host: the launcher, the Accounts page.</summary>
    private readonly IWidgetHost _widgetHost;

    public RecentFilesView(IWidgetHost host)
    {
        _widgetHost = host;
        _scroll = new ScrollViewer
        {
            Content = _list,
            Padding = WidgetLayout.ListInset,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        _fade = new ScrollFade(_scroll, _list);
        _empty = Fluent.EmptyState(RecentFilesModule.Glyph, _emptyText, out _);

        Children.Add(_scroll);
        Children.Add(_empty);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        Strings.Changed += OnLanguage;

        // A debug render never reads the user's Recent Items: its PNG must not carry their file names.
        if (_surface?.IsPreview != true)
        {
            _items ??= _widgetHost.RecentFiles.Subscribe(OnItems);
        }

        _tick ??= ticker.Subscribe(((config as RecentFilesConfig) ?? new RecentFilesConfig()).TickInterval, Render);
    }

    public void ApplyConfig(WidgetConfig config) => Render();

    public void ApplySize(WidgetArea area)
    {
        var plan = RecentFilesLayout.Plan(area);
        _scale = XamlRoot?.RasterizationScale ?? 1;
        if (plan != _plan)
        {
            _plan = plan;
            Render();
        }
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _items?.Dispose();
        _items = null;
        _tick?.Dispose();
        _tick = null;
        ClearRows();
        _fade.Dispose();
    }

    private void OnLanguage(Locale locale) => Render();

    private void OnItems()
    {
        if (_detached)
        {
            return;
        }

        _files = _widgetHost.RecentFiles.Current ?? [];
        _unreadable = _widgetHost.RecentFiles.Unreadable && _widgetHost.RecentFiles.Current is null;
        Render();
    }

    // -----------------------------------------------------------------------------------------
    // Drawing
    // -----------------------------------------------------------------------------------------

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        ClearRows();

        if (_files.Count == 0)
        {
            _emptyText.Text = (_unreadable ? RecentFilesStr.RecentFiles_Unreadable : RecentFilesStr.RecentFiles_Empty).Text();
            _empty.Visibility = Visibility.Visible;
            _scroll.Visibility = Visibility.Collapsed;
            _cursor = -1;
            return;
        }

        _empty.Visibility = Visibility.Collapsed;
        _scroll.Visibility = Visibility.Visible;
        var now = DateTimeOffset.Now;

        foreach (var file in _files)
        {
            var row = BuildRow(file, now);
            _rows.Add(row);
            _list.Children.Add(row);
        }

        _cursor = _cursor < _rows.Count ? _cursor : -1;
        ShowCursor();
        _fade.Refresh();
    }

    private Border BuildRow(RecentFile file, DateTimeOffset now)
    {
        // The shell's icon for the file; a page glyph until it comes, and for a file that has none.
        var image = new Image { Width = Fluent.IconSize, Height = Fluent.IconSize };
        var fallback = Fluent.Icon(Glyphs.File);
        var icon = new Grid { Width = Fluent.IconSize, Height = Fluent.IconSize, Margin = new Thickness(0, 0, Fluent.Space3, 0), VerticalAlignment = VerticalAlignment.Center, Children = { fallback, image } };
        var entry = new FolderEntry(file.Name, file.Path, false, 0, file.Opened, false);
        var pixels = (int)Math.Ceiling(Fluent.IconSize * _scale);
        if (_widgetHost.FileIcons.TryIcon(entry, pixels, out var known))
        {
            ShowIcon(image, fallback, known);
        }
        else
        {
            _ = LoadIconAsync(image, fallback, entry, pixels);
        }

        var name = Fluent.Text(FluentType.Body);
        name.Text = file.Name;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.MaxLines = 1;

        var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name } };
        if (_plan.TwoLine)
        {
            var folder = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            folder.Text = _plan.TimeColumn ? file.Folder : $"{file.Folder} · {Formatting.Ago(file.Opened, now)}";
            folder.TextTrimming = TextTrimming.CharacterEllipsis;
            folder.MaxLines = 1;
            body.Children.Add(folder);
        }

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        SetColumn(body, 1);
        grid.Children.Add(icon);
        grid.Children.Add(body);

        if (_plan.TimeColumn)
        {
            var when = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            when.Text = Formatting.Ago(file.Opened, now);
            when.TextAlignment = TextAlignment.Right;
            when.MinWidth = TimeWidth;
            when.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
            when.VerticalAlignment = VerticalAlignment.Center;
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(when, FontNumeralAlignment.Tabular);
            SetColumn(when, 2);
            grid.Children.Add(when);
        }

        var row = Fluent.Row(grid, _plan.Compact);
        if (_plan.TwoLine)
        {
            row.Padding = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, Fluent.Space2);
        }

        RowFocus.Prepare(row);
        row.Tag = file;
        row.Tapped += OnRowTapped;
        _rowHandles.Add(Fluent.Hover(row));
        _rowHandles.Add(ItemMenu.Attach(row, () => RowMenu(file)));
        ToolTipService.SetToolTip(row, $"{file.Path}{Environment.NewLine}{LocaleFormat.Date(Strings.Current, file.Opened.LocalDateTime, DateStyle.Medium)} {LocaleFormat.Time(Strings.Current, file.Opened.LocalDateTime)}");
        return row;
    }

    private async Task LoadIconAsync(Image target, FontIcon fallback, FolderEntry entry, int pixels)
    {
        // AppIcons logs and returns null for a file without an icon; nothing here throws.
        ShowIcon(target, fallback, await _widgetHost.FileIcons.IconAsync(entry, pixels).ConfigureAwait(true));
    }

    private static void ShowIcon(Image target, FontIcon fallback, ImageSource? source)
    {
        target.Source = source;
        fallback.Visibility = source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private IEnumerable<ItemMenuEntry> RowMenu(RecentFile file)
    {
        if (_detached)
        {
            return [];
        }

        return
        [
            new(Glyphs.Open, Str.Common_Open.Text(), () => Open(file)),
            new(Glyphs.Folder, Str.Shortcut_OpenFileLocation.Text(), () => Reveal(file)),
            ItemMenuEntry.Separator,
            new(Glyphs.Clear, RecentFilesStr.RecentFiles_Remove.Text(), () => _widgetHost.RecentFiles.Remove(file)),
        ];
    }

    private void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_surface?.EditMode == true || sender is not Border { Tag: RecentFile file })
        {
            return;
        }

        args.Handled = true;
        _cursor = _rows.IndexOf((Border)sender);
        ShowCursor();
        Open(file);
    }

    private void Open(RecentFile file)
    {
        if (_surface?.IsPreview == true)
        {
            return;
        }

        var outcome = _widgetHost.Launcher.Open(file.Path);
        if (outcome == LaunchOutcome.Missing)
        {
            // Gone since the last read: read again, and the row goes.
            _widgetHost.RecentFiles.Refresh();
        }
    }

    private void Reveal(RecentFile file)
    {
        if (_surface?.IsPreview != true)
        {
            _widgetHost.FileOps.ShowInExplorer([file.Path]);
        }
    }

    private void ClearRows()
    {
        foreach (var handle in _rowHandles)
        {
            handle.Dispose();
        }

        _rowHandles.Clear();

        foreach (var row in _rows)
        {
            row.Tapped -= OnRowTapped;
            row.Tag = null;
        }

        _rows.Clear();
        _list.Children.Clear();
    }

    // -----------------------------------------------------------------------------------------
    // Keyboard ([S-1288])
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-1564] An empty list takes no keyboard.</summary>
    public bool WantsKeys => _files.Count > 0;

    /// <summary>[S-4845] Up/Down, Page Up/Down and Home/End move; Enter opens; Delete takes it off the list; Menu opens its menu.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var decision = ListKeys.Decide(key, _cursor, _files.Count);
        switch (decision.Command)
        {
            case ListCommand.Focus:
                _cursor = decision.Index;
                ShowCursor();
                return true;

            case ListCommand.Open:
                Open(_files[decision.Index]);
                return true;

            case ListCommand.Delete:
                _cursor = ListKeys.AfterRemoval(decision.Index, _files.Count - 1);
                _widgetHost.RecentFiles.Remove(_files[decision.Index]);
                return true;

            case ListCommand.Menu when decision.Index < _rows.Count:
                ItemMenu.Open(_rows[decision.Index], RowMenu(_files[decision.Index]));
                return true;

            case ListCommand.Release:
                var had = _cursor >= 0;
                _cursor = -1;
                ShowCursor();
                return had;

            default:
                return false;
        }
    }

    private void ShowCursor()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            RowFocus.Mark(_rows[i], i == _cursor);
        }
    }
}
