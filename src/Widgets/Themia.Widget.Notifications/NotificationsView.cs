using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Notifications;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4400] Windows' notification list on the family's rows ([S-6711]), as Windows 11's notification
/// center groups it (Karpo §19, §100): a notification's title primary, when it came as the value, its
/// body on the second line; grouped by app under a heading (its icon, its name) from an expanded height,
/// each app's newest three and a "See more (n)" row. A click focuses a row, a double-click opens the
/// app; Clear all is the header's button beside the neutral count.
/// <para>
/// [S-4404] The ladder (<see cref="NotificationsLayout"/>): the list at every size, the body from a
/// regular tile, the headings from an expanded height. The list is the host's one feed's
/// (<see cref="IWidgetHost.Notifications"/>), which follows Windows' changes; access Windows has not
/// given is the state row's sentence and the one button that fixes it ([S-4403]).
/// </para>
/// </summary>
internal sealed partial class NotificationsView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private const string Scope = "notifications";

    /// <summary>Where Windows keeps notification access: Settings › Privacy › Notifications.</summary>
    private static readonly Uri AccessPage = new("ms-settings:privacy-notifications");

    /// <summary>[S-6710] The header's extras: the neutral count and Clear all.</summary>
    private readonly KitHeader _header;
    private readonly StackPanel _list = new();
    private readonly ScrollViewer _scroll;
    private ScrollFade? _fade;
    private readonly KitStatePanel _state;

    /// <summary>One per row, so its handlers come off with it.</summary>
    private readonly List<IDisposable> _rowHandles = [];

    /// <summary>The rows a key can land on, in order, with the notification each stands for.</summary>
    private readonly List<(RowBlock Row, NotificationItem Item)> _rows = [];

    /// <summary>The bottom of each line of the list and whether it is a heading, for the whole-line cap ([S-6712]).</summary>
    private readonly List<(double Bottom, bool Heading)> _lines = [];

    /// <summary>The apps whose group shows every notification ("See more" opened); the showing's, never saved.</summary>
    private readonly HashSet<string> _open = new(StringComparer.OrdinalIgnoreCase);

    private IWidgetSurface? _surface;
    private IDisposable? _lease;
    private Ticker? _ticker;
    private IDisposable? _minutes;
    private NotificationPlan _plan = NotificationsLayout.Plan(default);
    private WidgetArea _area;
    private NotificationSnapshot _preview = NotificationSnapshot.Pending;
    private int? _cursor;
    private bool _detached = true;

    /// <summary>[S-6913] What the view asks of the host: the notification feed and the launcher.</summary>
    private readonly IWidgetHost _widgetHost;

    public NotificationsView(IWidgetHost host)
    {
        _widgetHost = host;
        _header = new KitHeader(host);
        _scroll = new ScrollViewer
        {
            Content = _list,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _state = new KitStatePanel(NotificationsModule.Glyph, host) { Visibility = Visibility.Collapsed };

        Children.Add(_scroll);
        Children.Add(_state);
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _header;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _ticker = ticker;

        // [S-1251] A debug render never reads the person's notifications.
        if (_surface is not { IsPreview: true })
        {
            _lease = _widgetHost.Notifications.Acquire(Render);
        }
#if DEBUG
        else if (_preview.Access == NotificationAccess.Pending)
        {
            _preview = new NotificationSnapshot(NotificationAccess.Allowed, Sample(5));
        }
#endif

        _fade ??= new ScrollFade(_scroll, _list);
        PointerEntered += OnPointerEntered;
        Strings.Changed += OnLanguage;

        // "4m" goes stale on its own: once a minute is the slowest cadence that keeps it true.
        _minutes = ticker.Subscribe(TimeSpan.FromMinutes(1), Render);
        Render();
    }

    public void ApplyConfig(WidgetConfig config) => Render();

    /// <summary>[S-4404] [S-663] A new size: the ladder again, and the whole lines that fit.</summary>
    public void ApplySize(WidgetArea area)
    {
        var plan = NotificationsLayout.Plan(area);
        var moved = plan != _plan || area.Height != _area.Height;
        _area = area;
        if (!moved)
        {
            return;
        }

        _plan = plan;
        Render();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        PointerEntered -= OnPointerEntered;
        Strings.Changed -= OnLanguage;
        _minutes?.Dispose();
        _minutes = null;
        _lease?.Dispose();
        _lease = null;
        _fade?.Dispose();
        _fade = null;
        ClearRows();
        _header.Detach();
        _state.Detach();
        _open.Clear();
        _ticker = null;
    }

    private NotificationSnapshot Snapshot => _lease is null ? _preview : _widgetHost.Notifications.Snapshot;

    // ---- drawing -----------------------------------------------------------------------------

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        ClearRows();
        var snapshot = Snapshot;
        var items = snapshot.Items;
        var allowed = snapshot.Access == NotificationAccess.Allowed;

        // [S-4402] Clear all is the header's button, beside how many there are.
        _header.Show(
            [],
            busy: false,
            count: allowed ? items.Count : null,
            buttons: allowed && items.Count > 0 ? [new KitHeaderButton(Glyphs.Clear, NotificationsStr.Panels_Notifications_ClearAll.Text(), ClearAll)] : []);

        _scroll.Visibility = Visibility.Collapsed;
        _state.Hide();

        if (snapshot.Access == NotificationAccess.Pending)
        {
            // Not answered yet: the frame, never a wrong "nothing" (§11).
            return;
        }

        if (!allowed)
        {
            ShowAccess(snapshot.Access);
            return;
        }

        if (items.Count == 0)
        {
            _state.Show(NotificationsStr.Panels_Notifications_None.Text(), offerAccounts: false);
            _cursor = null;
            return;
        }

        _scroll.Visibility = Visibility.Visible;
        var now = DateTimeOffset.Now;

        if (_plan.Grouped)
        {
            foreach (var group in NotificationRules.Groups(items))
            {
                AddLine(Heading(group), FamilyShape.Row1, heading: true);
                var (rows, more) = NotificationRules.Fold(group, _open.Contains(group.AppId), _plan.Folded);
                foreach (var item in rows)
                {
                    AddRow(item, now, withIcon: false);
                }

                if (more > 0 || (_open.Contains(group.AppId) && group.Items.Count > _plan.Folded))
                {
                    AddLine(Fold(group, more), FamilyShape.Row1, heading: false);
                }
            }
        }
        else
        {
            foreach (var item in NotificationRules.Newest(items))
            {
                AddRow(item, now, withIcon: true);
            }
        }

        // [S-6712] As tall as the whole lines that fit, never ending on a heading.
        var room = KitLadder.ListHeight(_area);
        var bottoms = _lines.Select(l => l.Bottom).ToList();
        var content = bottoms.Count > 0 ? bottoms[^1] : 0;
        _scroll.Height = !_area.IsMeasured ? double.NaN
            : content <= room + Disclosure.Tolerance ? content
            : KitChecks.WholeLineCap(bottoms, room, [.. _lines.Select(l => l.Heading)]);

        _cursor = _cursor is { } cursor && cursor < _rows.Count ? cursor : null;
        ShowCursor();
        _fade?.Refresh();
    }

    private void AddLine(FrameworkElement line, double height, bool heading)
    {
        _list.Children.Add(line);
        _lines.Add(((_lines.Count > 0 ? _lines[^1].Bottom : 0) + height, heading));
    }

    /// <summary>[S-4403] Access Windows has not given: the state row's sentence and the one button that fixes it.</summary>
    private void ShowAccess(NotificationAccess access)
    {
        switch (access)
        {
            case NotificationAccess.Denied:
                _state.Show(NotificationsStr.Panels_Notifications_Denied.Text(), offerAccounts: false, action: (NotificationsStr.Panels_Notifications_OpenSettings.Text(), OpenAccessPage), problem: true);
                break;

            case NotificationAccess.Unspecified:
                _state.Show(NotificationsStr.Panels_Notifications_Unspecified.Text(), offerAccounts: false, action: (NotificationsStr.Panels_Notifications_Allow.Text(), () => _ = AskAsync()));
                break;

            default:
                _state.Show(NotificationsStr.Panels_Notifications_Unsupported.Text(), offerAccounts: false);
                break;
        }
    }

    /// <summary>[S-4402] An app's heading: one row of 32, its icon and its name in BodyStrong, secondary.</summary>
    private Grid Heading(NotificationGroup group)
    {
        var icon = AppIcon(group.AppId);
        icon.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        var name = Family.Text(FamilyType.BodyStrong, ColourRole.TextSecondary, group.AppName);
        name.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(name, 1);

        return new Grid
        {
            Height = FamilyShape.Row1,
            Padding = Family.SideInset,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { icon, name },
        };
    }

    /// <summary>[S-4402] "See more (n)" / "See fewer": a row of the list's own, its name secondary at the rows' text edge.</summary>
    private RowBlock Fold(NotificationGroup group, int more)
    {
        var row = new RowBlock(RowKind.List, _plan.Lead ? FamilyShape.Icon : 0) { Interactive = true };
        row.Show(more > 0 ? NotificationsStr.Panels_Notifications_SeeMore.Format(("count", LocaleFormat.Integer(more))) : NotificationsStr.Panels_Notifications_SeeFewer.Text());
        row.NameText.Foreground = Family.Brush(ColourRole.TextSecondary);

        var appId = group.AppId;
        TappedEventHandler onTap = (_, args) =>
        {
            args.Handled = true;
            if (!_open.Remove(appId))
            {
                _open.Add(appId);
            }

            Render();
        };
        row.Tapped += onTap;
        _rowHandles.Add(new Unhook(() =>
        {
            row.Tapped -= onTap;
            row.Detach();
        }));
        return row;
    }

    /// <summary>
    /// One notification as a Row block: the title, when it came, the body (on one line, its own breaks
    /// read as spaces) on the second line where the plan gives two; flat, its app's icon as the lead.
    /// </summary>
    private void AddRow(NotificationItem item, DateTimeOffset now, bool withIcon)
    {
        var row = new RowBlock(RowKind.List, _plan.Lead ? FamilyShape.Icon : 0) { Lines = _plan.Lines, Interactive = true };
        var title = item.Title.Length > 0 ? item.Title : NotificationRules.FallbackName(item.AppId);
        row.Show(title, _plan.Time ? Formatting.Ago(item.Arrived, now) : null, item.Body.Length > 0 ? ClipboardFormatting.Preview(item.Body, NotificationRules.MostBody) : null);
        if (withIcon && _plan.Lead)
        {
            row.Lead(AppIcon(item.AppId));
        }

        _rowHandles.Add(ItemMenu.Attach(row, () => RowMenu(item)));
        WidgetItems.Mark(row); // [S-814]

        // [S-7065] A click focuses the row; a double-click opens the sender, as Enter does.
        TappedEventHandler onTap = (_, args) =>
        {
            args.Handled = true;
            Focus(row);
        };
        DoubleTappedEventHandler onDoubleTap = (_, args) =>
        {
            args.Handled = true;
            Open(item);
        };

        row.Tapped += onTap;
        row.DoubleTapped += onDoubleTap;
        _rowHandles.Add(new Unhook(() =>
        {
            row.Tapped -= onTap;
            row.DoubleTapped -= onDoubleTap;
            row.Detach();
        }));

        var app = item.AppName.Length > 0 ? item.AppName : NotificationRules.FallbackName(item.AppId);
        AutomationProperties.SetName(row, $"{app}: {title}");
        ToolTipService.SetToolTip(row, NotificationsStr.Panels_Notifications_Open.Format(("app", app)));

        _rows.Add((row, item));
        AddLine(row, _plan.RowHeight, heading: false);
    }

    /// <summary>[S-4405] The sender's icon, 16: the bell until one comes, and where none does.</summary>
    private Grid AppIcon(string appId)
    {
        var host = new Grid { Width = FamilyShape.Icon, Height = FamilyShape.Icon, VerticalAlignment = VerticalAlignment.Center };
        var fallback = Family.Glyph(NotificationsModule.Glyph);
        host.Children.Add(fallback);

        if (_lease is not null && NotificationRules.AppTarget(appId) is { } target)
        {
            _ = LoadIconAsync(host, fallback, target);
        }

        return host;
    }

    private async Task LoadIconAsync(Grid host, FontIcon fallback, string target)
    {
        try
        {
            var image = await AppIconSource.GetAsync(target, (int)Math.Ceiling(FamilyShape.Icon * 2), AppIconSource.ThemeOf(this)).ConfigureAwait(true);
            if (image is null || _detached)
            {
                return;
            }

            host.Children.Remove(fallback);
            host.Children.Add(new Image { Source = image, Width = FamilyShape.Icon, Height = FamilyShape.Icon, Stretch = Stretch.Uniform });
        }
        catch (Exception ex)
        {
            // The bell stays: an icon is a nicety, never a reason for the row to go.
            Log.Debug(Scope, $"no icon for a sender ({ex.GetType().Name})");
        }
    }

    private void ClearRows()
    {
        foreach (var handle in _rowHandles)
        {
            handle.Dispose();
        }

        _rowHandles.Clear();
        _rows.Clear();
        _lines.Clear();
        _list.Children.Clear();
    }

    // ---- acting ------------------------------------------------------------------------------

    /// <summary>In edit mode nothing is opened or cleared ([S-4409]).</summary>
    private bool Inert => _detached || _surface?.EditMode == true;

    private IEnumerable<ItemMenuEntry> RowMenu(NotificationItem item)
    {
        if (Inert)
        {
            return [];
        }

        var app = item.AppName.Length > 0 ? item.AppName : NotificationRules.FallbackName(item.AppId);
        return
        [
            new(Glyphs.OpenWith, NotificationsStr.Panels_Notifications_Open.Format(("app", app)), () => Open(item)),
            new(Glyphs.Dismiss, NotificationsStr.Panels_Notifications_Dismiss.Text(), () => Dismiss(item), Quick: QuickCommand.Delete),
            ItemMenuEntry.Separator,
            new(Glyphs.Clear, NotificationsStr.Panels_Notifications_ClearApp.Format(("app", app)), () => ClearApp(item.AppId), Danger: true),
        ];
    }

    private void Open(NotificationItem item)
    {
        if (Inert || _lease is null)
        {
            return;
        }

        if (_widgetHost.Launcher.OpenApp(item.AppId) != LaunchOutcome.Opened)
        {
            Log.Info(Scope, "the sender of a notification could not be opened");
        }
    }

    private void Dismiss(NotificationItem item)
    {
        if (Inert)
        {
            return;
        }

        if (_lease is null)
        {
            _preview = _preview with { Items = [.. _preview.Items.Where(each => each.Id != item.Id)] };
            Render();
            return;
        }

        _widgetHost.Notifications.Dismiss(item.Id);
    }

    private void ClearApp(string appId)
    {
        if (Inert)
        {
            return;
        }

        if (_lease is null)
        {
            _preview = _preview with { Items = [.. _preview.Items.Where(each => !string.Equals(each.AppId, appId, StringComparison.OrdinalIgnoreCase))] };
            Render();
            return;
        }

        _widgetHost.Notifications.DismissApp(appId);
    }

    private void ClearAll()
    {
        if (Inert)
        {
            return;
        }

        if (_lease is null)
        {
            _preview = _preview with { Items = [] };
            Render();
            return;
        }

        _widgetHost.Notifications.ClearAll();
    }

    private void OpenAccessPage()
    {
        if (!Inert && _lease is not null)
        {
            _ = _widgetHost.Launcher.OpenWindowsSettings(AccessPage);
        }
    }

    private async Task AskAsync()
    {
        if (Inert || _lease is null)
        {
            return;
        }

        try
        {
            var answer = await _widgetHost.Notifications.RequestAccessAsync().ConfigureAwait(true);
            if (answer == NotificationAccess.Denied && !_detached)
            {
                // Said no (or could not ask): the page where it can be turned on.
                _ = _widgetHost.Launcher.OpenWindowsSettings(AccessPage);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "notification access could not be asked for", ex);
        }
    }

    /// <summary>[S-4403] Access has no change event: it is looked at again when the pointer comes onto the widget.</summary>
    private void OnPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (_lease is not null)
        {
            _widgetHost.Notifications.Refresh();
        }
    }

    // ---- keys ([S-4409]) ---------------------------------------------------------------------

    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var decision = ListKeys.Decide(key, _cursor ?? -1, _rows.Count);
        switch (decision.Command)
        {
            case ListCommand.Focus:
                _cursor = decision.Index;
                ShowCursor();
                return true;

            case ListCommand.Open:
                Open(_rows[decision.Index].Item);
                return true;

            case ListCommand.Delete:
                _cursor = ListKeys.AfterRemoval(decision.Index, _rows.Count - 1) is var next and >= 0 ? next : null;
                Dismiss(_rows[decision.Index].Item);
                return true;

            case ListCommand.Menu when decision.Index < _rows.Count:
                ItemMenu.Open(_rows[decision.Index].Row, RowMenu(_rows[decision.Index].Item));
                return true;

            case ListCommand.Release:
                var had = _cursor is not null;
                _cursor = null;
                ShowCursor();
                return had;

            default:
                return false;
        }
    }

    /// <summary>[S-1564] Rows to move through, and not in edit mode.</summary>
    public bool WantsKeys => !Inert && _rows.Count > 0;

    private void Focus(RowBlock row)
    {
        var index = _rows.FindIndex(r => ReferenceEquals(r.Row, row));
        if (index < 0 || Inert)
        {
            return;
        }

        _cursor = index;
        ShowCursor();
    }

    /// <summary>[S-1288] The keyboard's ring: the Row block's own.</summary>
    private void ShowCursor()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Row.Focused = _cursor == i;
        }
    }

    private void OnLanguage(Locale locale) => Render();

    private sealed class Unhook(Action undo) : IDisposable
    {
        private Action? _undo = undo;

        public void Dispose()
        {
            _undo?.Invoke();
            _undo = null;
        }
    }
}
