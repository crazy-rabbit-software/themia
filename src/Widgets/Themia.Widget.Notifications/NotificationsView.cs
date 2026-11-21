using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Notifications;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4400] Windows' notification list as a kit list ([S-7074]) over the host's one feed
/// (<see cref="IWidgetHost.Notifications"/>, which follows Windows' changes), shaped by <see cref="NotificationsSpec"/>:
/// each app's icon as the rich look's lead, or grouped under app headings with the icon before the name ([S-4402]). A
/// click focuses a row, a double-click opens the app; a "See more" row opens on the click. Access Windows has not given
/// is the kit's state row with the one button that fixes it ([S-4403], [S-7076]); Clear all is the card menu's line.
/// </summary>
internal sealed partial class NotificationsView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    private const string Scope = "notifications";

    /// <summary>Where Windows keeps notification access: Settings › Privacy › Notifications.</summary>
    private static readonly Uri AccessPage = new("ms-settings:privacy-notifications");

    /// <summary>"4m" goes stale on its own: once a minute is the slowest cadence that keeps it true.</summary>
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    /// <summary>[S-6913] What the view asks of the host: the notification feed and the launcher.</summary>
    private readonly IWidgetHost _widgetHost;
    private readonly KitBinding _binding;
    private readonly NotificationSource _source;

    /// <summary>The fold rows a click was hooked onto, forgotten with the rows.</summary>
    private readonly HashSet<FrameworkElement> _folds = [];

    private NotificationsConfig _config = new();
    private IWidgetSurface? _surface;
    private bool _attached;

    public NotificationsView(IWidgetHost host)
    {
        _widgetHost = host;
        _source = new NotificationSource(this);
        _binding = new KitBinding(host, NotificationsSpec.Spec, NotificationsModule.Glyph, item => NotificationsSpec.Actions(item, _source.Of(item.Id)), Run)
        {
            Look = new KitRichLook
            {
                Lead = item => item.Lead.Value is { } app ? AppIcon(app) : null,
                HeadingLead = group => AppIcon(group.Key),
                Touch = Quiet,
            },
            Decorate = HookFold,
            DecorateHeading = LinkHeading,
            Redrawing = ForgetRows,
        };

        Children.Add(_binding.List);
        Children.Add(_binding.State);
        _binding.Status.Place(this);
    }

    /// <summary>[S-4402] [S-12481] Clear all, on the card menu while Windows allows reading and there is something to clear.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => _source.Snapshot is { Access: NotificationAccess.Allowed, Items.Count: > 0 }
            ? [new WidgetAction(Glyphs.Clear, NotificationsStr.Panels_Notifications_ClearAll.Text(), ClearAll)]
            : [];

    public void Bind(IWidgetSurface surface) => _surface = surface;

    /// <summary>A debug render attached: its notifications (Preview/; nothing in other builds).</summary>
    partial void PreviewAttached();

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as NotificationsConfig ?? new NotificationsConfig();
        _attached = true;

        // [S-1251] A debug render never reads the person's notifications.
        if (_surface is not { IsPreview: true })
        {
            _source.Start(_widgetHost.Notifications);
        }
        else
        {
            PreviewAttached();
        }

        _links.Join(_surface?.Links);
        PointerEntered += OnPointerEntered;
        _binding.Attach(ticker, _source, _config, Minute);
    }

    /// <summary>[S-11404] The config's lines and headings.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as NotificationsConfig ?? new NotificationsConfig();
        if (_attached)
        {
            _binding.Apply(_source, _config, Minute, show: false);
        }
    }

    public void ApplySize(WidgetArea area) => _binding.Size(area);

    public bool HandleKey(WidgetKey key) => !Inert && _binding.HandleKey(key);

    /// <summary>[S-1564] Rows to move through, and not in edit mode.</summary>
    public bool WantsKeys => !Inert && _binding.List.WantsKeys;

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        PointerEntered -= OnPointerEntered;
        _binding.Dispose();
        _source.Stop();
        _links.Leave();
        _folds.Clear();
    }

    /// <summary>In edit mode nothing is opened or cleared ([S-4409]).</summary>
    private bool Inert => !_attached || _surface?.EditMode == true;

    // ---- acting ------------------------------------------------------------------------------

    private void Run(KitItem item, KitAction action)
    {
        if (Inert)
        {
            return;
        }

        if (NotificationsSpec.FoldOf(item) is { } app)
        {
            _source.Fold(app);
            return;
        }

        if (_source.Of(item.Id) is not { } notification)
        {
            return;
        }

        switch (action.Id)
        {
            case NotificationsSpec.OpenId when _source.Live && _widgetHost.Launcher.OpenApp(notification.AppId) != LaunchOutcome.Opened:
                Log.Info(Scope, "the sender of a notification could not be opened");
                break;

            case NotificationsSpec.DismissId:
                _source.Dismiss(notification);
                break;

            case NotificationsSpec.ClearAppId:
                _source.ClearApp(notification.AppId);
                break;
        }
    }

    private void ClearAll()
    {
        if (!Inert)
        {
            _source.ClearAll();
        }
    }

    /// <summary>[S-4402] "See more" opens on the click, as Windows' own does: its row's click is the fold's.</summary>
    private void HookFold(KitItem item, FrameworkElement row)
    {
        if (NotificationsSpec.FoldOf(item) is not { } app || !_folds.Add(row))
        {
            return;
        }

        row.Tapped += (_, _) =>
        {
            if (!Inert)
            {
                _source.Fold(app);
            }
        };
    }

    /// <summary>[S-4402] A fold row's words in the secondary role, at the rows' text edge.</summary>
    private static void Quiet(KitItem item, RowBlock row)
    {
        if (NotificationsSpec.FoldOf(item) is not null)
        {
            row.NameText.Foreground = Family.Brush(ColourRole.TextSecondary);
        }
    }

    private void ForgetRows()
    {
        _folds.Clear();
        Apps.Clear();
    }

    private void OpenAccessPage()
    {
        if (!Inert && _source.Live)
        {
            _ = _widgetHost.Launcher.OpenWindowsSettings(AccessPage);
        }
    }

    private async void Ask()
    {
        if (Inert || !_source.Live)
        {
            return;
        }

        try
        {
            var answer = await _widgetHost.Notifications.RequestAccessAsync().ConfigureAwait(true);
            if (answer == NotificationAccess.Denied && _attached)
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
        if (_source.Live)
        {
            _widgetHost.Notifications.Refresh();
        }
    }

    /// <summary>[S-4405] The sender's icon, 16: the bell until one comes, and where none does.</summary>
    private Grid AppIcon(string appId)
    {
        var host = new Grid { Width = FamilyShape.Icon, Height = FamilyShape.Icon, VerticalAlignment = VerticalAlignment.Center };
        var fallback = Family.Glyph(NotificationsModule.Glyph);
        host.Children.Add(fallback);

        if (_source.Live && NotificationRules.AppTarget(appId) is { } target)
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
            if (image is null || !_attached)
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

    /// <summary>
    /// [S-4400] The host's notification feed as a kit source: the list as the config shows it (flat, or under app
    /// headings with their folds), access Windows has not given as the state row with its way out ([S-4403]).
    /// </summary>
    private sealed class NotificationSource(NotificationsView owner) : IKitSource
    {
        private INotifications? _feed;
        private IDisposable? _lease;

        /// <summary>The apps whose group shows every notification ("See more" opened); the showing's, never saved.</summary>
        private readonly HashSet<string> _open = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>A render's or a sample's notifications in place of the person's.</summary>
        private NotificationSnapshot _preview = NotificationSnapshot.Pending;

        public event Action? Changed;

        public bool Busy => false;

        /// <summary>Whether it reads the person's notifications (not a render's).</summary>
        public bool Live => _lease is not null;

        public NotificationSnapshot Snapshot => _lease is not null && _feed is not null ? _feed.Snapshot : _preview;

        public void Start(INotifications feed)
        {
            _feed = feed;
            _lease ??= feed.Acquire(() => Changed?.Invoke());
        }

        public void Stop()
        {
            _lease?.Dispose();
            _lease = null;
            _open.Clear();
        }

        public void Show(bool refresh)
        {
            if (refresh && Live)
            {
                _feed?.Refresh();
            }
        }

        public KitRead Read()
        {
            var snapshot = Snapshot;
            var locale = Strings.Current;
            return snapshot.Access switch
            {
                // Not answered yet: the loading row, never a wrong "nothing" (§11).
                NotificationAccess.Pending => new KitRead([], []),
                NotificationAccess.Allowed => new KitRead(NotificationsSpec.Items(snapshot.Items, owner._config.AppHeadings, _open, locale), [new KitOutcome(null, FetchStatus.Ok)]),
                NotificationAccess.Denied => new KitRead([], [], Stated: new KitStated(NotificationsStr.Panels_Notifications_Denied.Text(locale))
                {
                    Fix = new KitFix(NotificationsStr.Panels_Notifications_OpenSettings.Text(locale), owner.OpenAccessPage),
                }),
                NotificationAccess.Unspecified => new KitRead([], [], Stated: new KitStated(NotificationsStr.Panels_Notifications_Unspecified.Text(locale), Problem: false)
                {
                    Fix = new KitFix(NotificationsStr.Panels_Notifications_Allow.Text(locale), owner.Ask),
                }),
                _ => new KitRead([], [], Stated: new KitStated(NotificationsStr.Panels_Notifications_Unsupported.Text(locale), Problem: false)),
            };
        }

        public NotificationItem? Of(string id) => Snapshot.Items.FirstOrDefault(n => NotificationsSpec.Id(n) == id);

        /// <summary>[S-4402] Open or close an app's group.</summary>
        public void Fold(string app)
        {
            if (!_open.Remove(app))
            {
                _open.Add(app);
            }

            Changed?.Invoke();
        }

        public void Dismiss(NotificationItem item)
        {
            if (Live)
            {
                _feed!.Dismiss(item.Id);
                return;
            }

            Hold(_preview with { Items = [.. _preview.Items.Where(each => each.Id != item.Id)] });
        }

        public void ClearApp(string appId)
        {
            if (Live)
            {
                _feed!.DismissApp(appId);
                return;
            }

            Hold(_preview with { Items = [.. _preview.Items.Where(each => !string.Equals(each.AppId, appId, StringComparison.OrdinalIgnoreCase))] });
        }

        public void ClearAll()
        {
            if (Live)
            {
                _feed!.ClearAll();
                return;
            }

            Hold(_preview with { Items = [] });
        }

        /// <summary>[S-11850] A sample's or a render's notifications, <paramref name="open"/> the app opened.</summary>
        public void Hold(NotificationSnapshot snapshot, string? open = null)
        {
            _preview = snapshot;
            if (open is not null)
            {
                _open.Clear();
                _open.Add(open);
            }

            Changed?.Invoke();
        }

        /// <summary>[S-11850] What a render shows before a state is asked for.</summary>
        public bool Pending => _preview.Access == NotificationAccess.Pending;
    }
}
