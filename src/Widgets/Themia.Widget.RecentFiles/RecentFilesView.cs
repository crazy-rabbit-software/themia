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
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.RecentFiles;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4840] The files opened last, as Windows lists them in Recent Items, newest first, on the
/// family's rows ([S-6711]): the file's icon as the lead, its name primary, when it was opened as the
/// value, its folder on the second line where the tile is regular both ways ([S-4842]). A click focuses
/// a row and a double-click opens the file, as every list ([S-7065]); the row menu opens its location
/// or takes it off the list. The list is the shared <see cref="RecentItems"/>, redrawn when its folder
/// changes; the minute tick only keeps "2h" honest. Keys through the lease, as every list ([S-1288]).
/// </summary>
internal sealed partial class RecentFilesView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private readonly StackPanel _list = new();
    private readonly ScrollViewer _scroll;
    private readonly ScrollFade _fade;
    private readonly KitStatePanel _state;
    private readonly List<Grid> _rows = [];

    /// <summary>[S-6627] Each row's selection chrome, by row: laid behind the row, so it moves nothing.</summary>
    private readonly List<Border> _chromes = [];

    /// <summary>The row under the pointer, or -1.</summary>
    private int _hovered = -1;
    private readonly List<IDisposable> _rowHandles = [];

    private IWidgetSurface? _surface;
    private IDisposable? _items;
    private IDisposable? _tick;
    private IReadOnlyList<RecentFile> _files = [];
    private bool _unreadable;
    private WidgetArea _area;
    private RecentFilesPlan _plan = RecentFilesLayout.Plan(default);
    private int _cursor = -1;
    private bool _detached;
    private double _scale = 1;

    /// <summary>[S-6913] What the view asks of the host: the recent items, the file icons, the launcher, the file operations.</summary>
    private readonly IWidgetHost _widgetHost;

    public RecentFilesView(IWidgetHost host)
    {
        _widgetHost = host;
        _scroll = new ScrollViewer
        {
            Content = _list,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _fade = new ScrollFade(_scroll, _list);
        _state = new KitStatePanel(RecentFilesModule.Glyph, host) { Visibility = Visibility.Collapsed };

        Children.Add(_scroll);
        Children.Add(_state);
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

    /// <summary>[S-4842] [S-663] A new size: draw again only when the ladder lands somewhere else or the rows that fit change.</summary>
    public void ApplySize(WidgetArea area)
    {
        var plan = RecentFilesLayout.Plan(area);
        var rows = RecentFilesLayout.Rows(area, plan);
        var moved = plan != _plan || rows != RecentFilesLayout.Rows(_area, _plan);
        _area = area;
        _scale = XamlRoot?.RasterizationScale ?? 1;
        if (moved)
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
        _state.Detach();
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
            // [S-6714] One row where the first file would be: the folder unread is a problem, an empty one is not.
            _state.Show((_unreadable ? RecentFilesStr.RecentFiles_Unreadable : RecentFilesStr.RecentFiles_Empty).Text(), offerAccounts: false, problem: _unreadable);
            _scroll.Visibility = Visibility.Collapsed;
            _cursor = -1;
            return;
        }

        _state.Hide();
        _scroll.Visibility = Visibility.Visible;
        var now = DateTimeOffset.Now;

        foreach (var file in _files)
        {
            var row = BuildRow(file, now);
            _rows.Add(row);
            _list.Children.Add(row);
        }

        // [S-6712] As tall as its whole rows: never a part of one at its foot.
        _scroll.Height = _area.IsMeasured ? Math.Min(_rows.Count, RecentFilesLayout.Rows(_area, _plan)) * _plan.RowHeight : double.NaN;

        _cursor = _cursor < _rows.Count ? _cursor : -1;
        ShowCursor();
        _fade.Refresh();
    }

    private Grid BuildRow(RecentFile file, DateTimeOffset now)
    {
        var block = new RowBlock(RowKind.List, _plan.Lead ? FamilyShape.Icon : 0) { Lines = _plan.Lines };
        block.Show(file.Name, _plan.Time ? Formatting.Ago(file.Opened, now) : null, file.Folder);

        if (_plan.Lead)
        {
            // The shell's icon for the file; a page glyph until it comes, and for a file that has none.
            var image = new Image { Width = FamilyShape.Icon, Height = FamilyShape.Icon };
            var fallback = Family.Glyph(Glyphs.File);
            var icon = new Grid { Width = FamilyShape.Icon, Height = FamilyShape.Icon, Children = { fallback, image } };
            var entry = new FolderEntry(file.Name, file.Path, false, 0, file.Opened, false);
            var pixels = (int)Math.Ceiling(FamilyShape.Icon * _scale);
            if (_widgetHost.FileIcons.TryIcon(entry, pixels, out var known))
            {
                ShowIcon(image, fallback, known);
            }
            else
            {
                _ = LoadIconAsync(image, fallback, entry, pixels);
            }

            block.Lead(icon);
        }

        // [S-6627] The focused file is marked as the desktop marks a selected one: the one selection
        // chrome, laid behind the row over the whole of it, so marking it moves nothing.
        var chrome = SelectionChrome.Wrap(new Grid());
        var row = new Grid { Children = { chrome, block } };
        _chromes.Add(chrome);

        WidgetItems.Mark(row); // [S-814] a press here is the row's, not the background's
        row.Tag = file;
        row.Tapped += OnRowTapped;
        row.DoubleTapped += OnRowDoubleTapped;
        var index = _chromes.Count - 1;
        _rowHandles.Add(SelectionChrome.Hover(row, over =>
        {
            _hovered = over ? index : _hovered == index ? -1 : _hovered;
            ShowCursor(bringIntoView: false);
        }));
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

    /// <summary>[S-4844] A click focuses the row.</summary>
    private void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is not Grid { Tag: RecentFile } row)
        {
            return;
        }

        args.Handled = true;
        _cursor = _rows.IndexOf(row);
        ShowCursor();
    }

    /// <summary>[S-7065] A double-click opens the file; the card makes the widget inert in edit mode ([S-1751]).</summary>
    private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        if (sender is not Grid { Tag: RecentFile file })
        {
            return;
        }

        args.Handled = true;
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
            row.DoubleTapped -= OnRowDoubleTapped;
            row.Tag = null;
        }

        _rows.Clear();
        _chromes.Clear();
        _hovered = -1;
        _list.Children.Clear();
    }

    // -----------------------------------------------------------------------------------------
    // Keyboard ([S-1288])
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-1564] An empty list takes no keyboard.</summary>
    public bool WantsKeys => _files.Count > 0;

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

    /// <summary>[S-6627] The focused row as the desktop marks a selected file, the one under the pointer as its hover.</summary>
    private void ShowCursor(bool bringIntoView = true)
    {
        for (var i = 0; i < _chromes.Count; i++)
        {
            SelectionChrome.Paint(_chromes[i], new SelectionCue(Selected: i == _cursor, Hovered: i == _hovered));
        }

        if (bringIntoView && _cursor >= 0 && _cursor < _rows.Count)
        {
            _rows[_cursor].StartBringIntoView();
        }
    }
}
