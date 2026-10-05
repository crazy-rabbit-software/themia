using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-1920]..[S-1926] A click on a widget opens its app: Task Manager from a meter, the sound
/// settings from the volume, the playing app from music. What opens is <see cref="WidgetLaunch"/>'s
/// decision; this file finds the click, shows the affordance (hand cursor, a faint hover, a tooltip)
/// and hands the target to the <see cref="IWidgetLauncher"/>.
/// <para>
/// The click is told apart from a double-click, which toggles edit mode ([S-65]): it opens once the
/// system's double-click time has passed without a second click. The widget's own controls keep
/// their clicks: a press on a button, slider, box, list item, a handled row or an
/// <see cref="IOwnsClicks"/> element is never an open.
/// </para>
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>The launcher; the shell's own outside tests.</summary>
    private readonly IWidgetLauncher _launcher = WidgetLauncher.Instance;

    /// <summary>The faint wash over the click area while the pointer is on it.</summary>
    private Border? _launchHover;

    /// <summary>Tooltip on the click area: the card for a body area, the header for a header area.</summary>
    private ToolTip? _launchTip;

    /// <summary>The target while the pointer is over the card; read fresh on entry and on the click.</summary>
    private WidgetLaunchTarget? _hoverTarget;

    /// <summary>Whether the pointer is over the click area (not on a control).</summary>
    private bool _overLaunch;

    /// <summary>[S-1925] The click waiting out the double-click time.</summary>
    private DispatcherQueueTimer? _launchTimer;

    private LaunchArea LaunchArea => WidgetLaunch.AreaOf(Model.Kind);

    private void AttachLaunch()
    {
        if (LaunchArea == LaunchArea.None || _host.IsPreview)
        {
            return;
        }

        _launchHover = new Border
        {
            Background = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Transparent),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        Children.Insert(Children.IndexOf(_editOutline), _launchHover);

        _launchTip = new ToolTip();
        _launchTip.Opened += OnLaunchTipOpened;
        ToolTipService.SetToolTip(LaunchArea == LaunchArea.Header ? _headerBand : this, _launchTip);

        Tapped += OnLaunchTapped;
        PointerEntered += OnLaunchPointerEntered;
        PointerExited += OnLaunchPointerGone;
        PointerCanceled += OnLaunchPointerGone;
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnLaunchPointerMoved), handledEventsToo: true);
    }

    private void DetachLaunch()
    {
        CancelLaunch();
        if (_launchTip is not null)
        {
            _launchTip.Opened -= OnLaunchTipOpened;
        }
    }

    /// <summary>The target a click opens now, or null.</summary>
    private WidgetLaunchTarget? LaunchTarget()
    {
        if (LaunchArea == LaunchArea.None || _host.IsPreview || IsFaulted || ShowsSetup)
        {
            return null;
        }

        try
        {
            var accounts = (_setupAccounts ?? AppServices.Accounts)?.Accounts
                .Select(a => new LaunchAccount(a.Id, a.Provider))
                .ToList() ?? [];

            return WidgetLaunch.For(Model.Kind, Model.Config, new WidgetLaunchContext
            {
                Accounts = accounts,
                HasProtocol = ShellLauncher.ProtocolRegistered,
                Locale = Strings.Current,
                Live = _running && _view is IWidgetLaunchFacts facts ? facts.LaunchFacts : WidgetLiveLaunch.None,
            });
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"could not work out what '{Model.Id}' ({Model.Kind}) opens", ex);
            return null;
        }
    }

    // ---- the click ---------------------------------------------------------------------------------

    private void OnLaunchTapped(object sender, TappedRoutedEventArgs e)
    {
        try
        {
            if (!IsLaunchClick(e.OriginalSource as DependencyObject) || LaunchTarget() is not { } target)
            {
                return;
            }

            e.Handled = true;
            CancelLaunch();

            _launchTimer = DispatcherQueue.CreateTimer();
            _launchTimer.IsRepeating = false;
            _launchTimer.Interval = TimeSpan.FromMilliseconds(DoubleClickMs());
            _launchTimer.Tick += (_, _) =>
            {
                CancelLaunch();
                Launch(target);
            };
            _launchTimer.Start();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the click on '{Model.Id}' ({Model.Kind}) failed", ex);
        }
    }

    /// <summary>[S-1925] A double-click is edit mode's, so the click it began opens nothing.</summary>
    private void CancelLaunch()
    {
        _launchTimer?.Stop();
        _launchTimer = null;
    }

    /// <summary>[S-1925] Enter on a widget holding the keyboard lease, when its view did not use it.</summary>
    private bool LaunchFromKey()
    {
        if (_host.EditMode || LaunchTarget() is not { } target)
        {
            return false;
        }

        Launch(target);
        return true;
    }

    private void Launch(WidgetLaunchTarget target)
    {
        if (_host.EditMode)
        {
            return;
        }

        Log.Info(Scope, $"'{Model.Id}' ({Model.Kind}) opens its {target.Method} target");
        WidgetLaunch.Open(target, _launcher);
    }

    /// <summary>Whether a press on <paramref name="source"/> is a click on the widget rather than on a control in it.</summary>
    private bool IsLaunchClick(DependencyObject? source)
    {
        var inHeader = false;
        var onControl = false;

        for (var node = source; node is not null && !ReferenceEquals(node, this); node = VisualTreeHelper.GetParent(node))
        {
            if (node is WidgetCard)
            {
                return false; // a card inside this one (none today) owns its own clicks
            }

            if (ReferenceEquals(node, _headerBand))
            {
                inHeader = true;
            }

            onControl |= IsControl(node);
        }

        return WidgetLaunch.OpensOnClick(_host.EditMode, _host.IsPreview, LaunchArea, inHeader, onControl);
    }

    /// <summary>The controls that take a click themselves.</summary>
    private static bool IsControl(DependencyObject node) => node is ButtonBase or ToggleSwitch or Thumb
        or TextBox or PasswordBox or RichEditBox or AutoSuggestBox or Selector or SelectorItem
        or IOwnsClicks
        || (node is RangeBase && node is not ProgressBar);

    private static double DoubleClickMs()
    {
        try
        {
            var ms = new Windows.UI.ViewManagement.UISettings().DoubleClickTime;
            return ms is > 0 and < 5000 ? ms : 500;
        }
        catch (Exception ex)
        {
            Log.Warn("card", "could not read the double-click time; using 500 ms", ex);
            return 500;
        }
    }

    // ---- the affordance ----------------------------------------------------------------------------

    private void OnLaunchPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hoverTarget = _host.EditMode ? null : LaunchTarget();
        var text = _hoverTarget is null ? null : WidgetLaunch.Text(_hoverTarget);

        if (_launchTip is not null)
        {
            _launchTip.Content = text;
        }

        if (LaunchArea == LaunchArea.Header)
        {
            AutomationProperties.SetName(_headerBand, text ?? string.Empty);
        }
        else
        {
            AutomationProperties.SetHelpText(this, text ?? string.Empty);
        }

        UpdateLaunchHover(e);
    }

    private void OnLaunchPointerMoved(object sender, PointerRoutedEventArgs e) => UpdateLaunchHover(e);

    private void OnLaunchPointerGone(object sender, PointerRoutedEventArgs e)
    {
        _hoverTarget = null;
        ShowLaunchHover(false);
    }

    private void UpdateLaunchHover(PointerRoutedEventArgs e)
    {
        try
        {
            ShowLaunchHover(_hoverTarget is not null && !_dragging && IsLaunchClick(e.OriginalSource as DependencyObject));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not update the open affordance", ex);
        }
    }

    /// <summary>[S-1922] Hand cursor and the faint wash over the click area, or neither.</summary>
    private void ShowLaunchHover(bool over)
    {
        if (over == _overLaunch || _launchHover is null)
        {
            return;
        }

        _overLaunch = over;

        if (over)
        {
            var radius = DesignLook.CornerRadius(_host.Design.CornerRadiusPx);
            var header = LaunchArea == LaunchArea.Header;
            var bottom = Model.Header == HeaderPlacement.Bottom;

            _launchHover.Margin = _surface.Margin;
            _launchHover.VerticalAlignment = !header ? VerticalAlignment.Stretch : bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            _launchHover.Height = header ? _headerBand.ActualHeight * _contentScale : double.NaN;
            _launchHover.CornerRadius = !header ? new CornerRadius(radius)
                : bottom ? new CornerRadius(0, 0, radius, radius)
                : new CornerRadius(radius, radius, 0, 0);
        }

        _launchHover.Visibility = over ? Visibility.Visible : Visibility.Collapsed;

        if (!_host.EditMode)
        {
            ProtectedCursor = InputSystemCursor.Create(over ? InputSystemCursorShape.Hand : InputSystemCursorShape.Arrow);
        }
    }

    /// <summary>[S-1925] Edit mode takes the click: the affordance goes and a waiting open is dropped.</summary>
    private void EndLaunchHover()
    {
        CancelLaunch();
        _hoverTarget = null;
        if (_launchHover is not null)
        {
            _overLaunch = false;
            _launchHover.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>The tooltip shows only over the click area: over a control without its own tip it stays shut.</summary>
    private void OnLaunchTipOpened(object sender, RoutedEventArgs e)
    {
        if (_launchTip is not null && (!_overLaunch || _hoverTarget is null || _host.EditMode))
        {
            _launchTip.IsOpen = false;
        }
    }
}
