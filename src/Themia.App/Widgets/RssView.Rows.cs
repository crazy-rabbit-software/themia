using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Themia.App.Widgets;

/// <summary>
/// The rows: built for the plan the ladder chose ([S-1305]), shown whole ([S-639]), with their site
/// icons ([S-1304]), their menu and the keyboard ([S-1306]).
/// </summary>
internal sealed partial class RssView
{
    /// <summary>Decoded site icons, one per origin, shared by this view's rows.</summary>
    private readonly Dictionary<string, Task<BitmapImage?>> _icons = new(StringComparer.OrdinalIgnoreCase);

    private void BuildRows(DateTimeOffset now)
    {
        ClearRows();

        var columns = Math.Max(1, _plan.Columns);

        _rows.ColumnDefinitions.Clear();
        _rows.RowDefinitions.Clear();

        for (var c = 0; c < columns; c++)
        {
            _rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var entries = _merged.Entries;

        for (var r = 0; r < (entries.Count + columns - 1) / columns; r++)
        {
            _rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var row = BuildRow(entries[i], i, now);
            SetRow(row, i / columns);
            SetColumn(row, i % columns);
            _rows.Children.Add(row);
            _rowElements.Add(row);
        }

        if (_cursor is { } cursor && cursor < _rowElements.Count)
        {
            RowFocus.Mark(_rowElements[cursor], true);
        }

        FitWholeRows(columns);
        _fade?.Refresh();
    }

    /// <summary>
    /// [S-639] The list is given the height of the rows that fit whole; the rest is a scroll away.
    /// Rows are as tall as their headlines, so they are measured at their column's width.
    /// </summary>
    private void FitWholeRows(int columns)
    {
        if (!_area.IsMeasured || _rowElements.Count == 0)
        {
            _scroll.Height = double.NaN;
            return;
        }

        var columnWidth = Math.Max(1, _area.Width / columns);
        var heights = new List<double>();

        for (var i = 0; i < _rowElements.Count; i++)
        {
            _rowElements[i].Measure(new Size(columnWidth, double.PositiveInfinity));
            var line = i / columns;

            if (line == heights.Count)
            {
                heights.Add(0);
            }

            heights[line] = Math.Max(heights[line], _rowElements[i].DesiredSize.Height);
        }

        var available = Math.Max(0, _area.Height - FeedLayout.ListInset);
        var shown = FeedLayout.WholeRows(heights, available);
        _scroll.Height = FeedLayout.ListInset + heights.Take(shown).Sum();
    }

    /// <summary>
    /// One article: its site (icon, and name where there is room), a headline that wraps to the
    /// plan's lines, the summary where the ladder affords it, and its age. A row as tall as its
    /// headline needs, not a fixed list row: trimming every headline to one line is how a feed
    /// reader becomes a list of sentence fragments.
    /// </summary>
    private Border BuildRow(FeedEntry entry, int index, DateTimeOffset now)
    {
        var stack = new StackPanel { Spacing = FeedLayout.LineGap };

        if (_plan.Host && FeedAggregate.SiteHost(entry.Link) is { Length: > 0 } host)
        {
            var source = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
            source.Children.Add(Favicon(entry.Link));
            var name = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            name.Text = host;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            source.Children.Add(name);
            stack.Children.Add(source);
        }

        var headline = Fluent.Text(FluentType.Body);
        headline.Text = entry.Title.Length > 0 ? entry.Title : Str.WidgetsC_Rss_Untitled.Text();
        headline.TextWrapping = TextWrapping.Wrap;
        headline.TextTrimming = TextTrimming.CharacterEllipsis;
        headline.MaxLines = _plan.HeadlineLines;
        stack.Children.Add(headline);

        if (_plan.PreviewLines > 0 && entry.Summary.Length > 0)
        {
            var preview = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            preview.Text = entry.Summary;
            preview.TextWrapping = TextWrapping.Wrap;
            preview.TextTrimming = TextTrimming.CharacterEllipsis;
            preview.MaxLines = _plan.PreviewLines;
            stack.Children.Add(preview);
        }

        var iconInline = _plan.Favicon && !_plan.Host && entry.Link.Length > 0;
        var date = _plan.Date && entry.Published is { } published ? FeedAggregate.Age(published, now) : null;

        if (iconInline || date is not null)
        {
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };

            if (iconInline)
            {
                meta.Children.Add(Favicon(entry.Link));
            }

            if (date is not null)
            {
                var when = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                when.Text = date;
                meta.Children.Add(when);
            }

            stack.Children.Add(meta);
        }

        var row = new Border
        {
            Child = stack,
            Padding = new Thickness(Fluent.Space4, FeedLayout.RowPadding, Fluent.Space4, FeedLayout.RowPadding),
            Tag = index,
        };

        ItemFill.Paint(row, ItemRole.Row);
        RowFocus.Prepare(row);
        _rowHooks.Add(Fluent.Hover(row));
        _rowHooks.Add(ItemMenu.Attach(row, () => MenuFor(entry)));

        if (entry.Link.Length > 0)
        {
            ToolTipService.SetToolTip(row, entry.Link);
        }

        row.Tapped += OnRowTapped;
        row.DoubleTapped += OnRowDoubleTapped;

        return row;
    }

    /// <summary>A click focuses the row.</summary>
    private void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is Border { Tag: int index })
        {
            FocusItem(index);
        }
    }

    private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        if (sender is Border { Tag: int index } && index < _merged.Entries.Count && _merged.Entries[index].Link.Length > 0)
        {
            args.Handled = true;
            Open(_merged.Entries[index].Link);
        }
    }

    /// <summary>[S-1306] The item's own menu: open, copy the link, refresh the feeds.</summary>
    private IEnumerable<ItemMenuEntry> MenuFor(FeedEntry entry) =>
    [
        new ItemMenuEntry(Glyphs.OpenExternal, Str.Common_Open.Text(), () => Open(entry.Link), Disabled: entry.Link.Length == 0),
        new ItemMenuEntry(Glyphs.Link, Str.WidgetsC_Rss_CopyLink.Text(), () =>
        {
            if (!ClipboardWatcher.SetText(entry.Link))
            {
                Log.Warn(Scope, "could not copy a link to the clipboard");
            }
        }, Disabled: entry.Link.Length == 0),
        ItemMenuEntry.Separator,
        new ItemMenuEntry(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: true)),
    ];

    /// <summary>
    /// [S-1306] [S-1568] A key from the shell's keyboard lease ([S-800], a click in the widget takes
    /// it): Up/Down (and Left/Right) step, Home/End jump, Enter opens, the Menu key opens the item's
    /// menu, Escape lets go. Returns whether the key was used.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || _merged.Entries.Count == 0)
        {
            return false;
        }

        if (key == WidgetKey.Menu)
        {
            if (_cursor is { } item && item < _merged.Entries.Count && item < _rowElements.Count)
            {
                ItemMenu.Open(_rowElements[item], MenuFor(_merged.Entries[item]));
                return true;
            }

            return false;
        }

        if (key == WidgetKey.Enter)
        {
            if (_cursor is { } at && at < _merged.Entries.Count && _merged.Entries[at].Link.Length > 0)
            {
                Open(_merged.Entries[at].Link);
                return true;
            }

            return false;
        }

        var had = _cursor;
        var next = ItemCursor.Move(_merged.Entries.Count, _cursor, key);

        if (next is { } index)
        {
            FocusItem(index);
        }
        else
        {
            Unfocus();
        }

        return had != next || key == WidgetKey.Escape && had is not null;
    }

    private void FocusItem(int index)
    {
        if (index < 0 || index >= _rowElements.Count)
        {
            return;
        }

        if (_cursor is { } old && old < _rowElements.Count)
        {
            RowFocus.Mark(_rowElements[old], false);
        }

        _cursor = index;
        RowFocus.Mark(_rowElements[index], true);
    }

    private void Unfocus()
    {
        if (_cursor is { } old && old < _rowElements.Count)
        {
            RowFocus.Mark(_rowElements[old], false);
        }

        _cursor = null;
    }

    /// <summary>
    /// [S-1304] The site's own icon, with a globe in its place until it arrives or when the site has
    /// none.
    /// </summary>
    private Grid Favicon(string link)
    {
        var globe = Fluent.Icon(Glyphs.Globe, "TextFillColorTertiaryBrush", 12);
        globe.HorizontalAlignment = HorizontalAlignment.Center;

        var image = new Image { Width = Fluent.IconSize, Height = Fluent.IconSize, Visibility = Visibility.Collapsed };
        var host = new Grid { Width = Fluent.IconSize, Height = Fluent.IconSize, VerticalAlignment = VerticalAlignment.Center };
        host.Children.Add(globe);
        host.Children.Add(image);

        _ = ShowIconAsync(link, image, globe);
        return host;
    }

    private async Task ShowIconAsync(string link, Image image, FontIcon globe)
    {
        if (SiteIcons.FaviconOf(link) is not { } icon)
        {
            return;
        }

        var origin = icon.GetLeftPart(UriPartial.Authority);

        if (!_icons.TryGetValue(origin, out var decoding))
        {
            decoding = DecodeIconAsync(link);
            _icons[origin] = decoding;
        }

        var bitmap = await decoding.ConfigureAwait(true);

        if (_detached || bitmap is null)
        {
            return;
        }

        image.ImageFailed += (_, _) =>
        {
            image.Visibility = Visibility.Collapsed;
            globe.Visibility = Visibility.Visible;
        };

        image.Source = bitmap;
        image.Visibility = Visibility.Visible;
        globe.Visibility = Visibility.Collapsed;
    }

    private static async Task<BitmapImage?> DecodeIconAsync(string link)
    {
        var bytes = await FaviconCache.GetAsync(link).ConfigureAwait(true);

        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            // Decoded at twice the drawn size, so the icon is sharp at 200% scale and no bigger.
            var bitmap = new BitmapImage { DecodePixelWidth = (int)(Fluent.IconSize * 2) };
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            // Bytes that passed the magic-number check and still would not decode: a broken icon,
            // and the globe stays.
            Log.Debug(Scope, $"a site icon would not decode: {ex.Message}");
            return null;
        }
    }

    private void ClearRows()
    {
        foreach (var hook in _rowHooks)
        {
            hook.Dispose();
        }

        _rowHooks.Clear();

        foreach (var row in _rowElements)
        {
            row.Tapped -= OnRowTapped;
            row.DoubleTapped -= OnRowDoubleTapped;
        }

        _rowElements.Clear();
        _rows.Children.Clear();
    }
}
