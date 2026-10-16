using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Shell;

namespace Themia.App.Shell.Config;

/// <summary>
/// The General page's desktop-integration rows: start with Windows ([S-933]), Windows' desktop icons
/// ([S-930]) and game mode ([S-920]).
/// <para>
/// All three show something Windows owns or decides, so each one reads Windows when it comes on
/// screen and listens while it is there, rather than trusting what Themia last wrote.
/// </para>
/// </summary>
internal static partial class ConfigPages
{
    /// <summary>
    /// [S-935] The startup switch and, under it, what to do when a change did not take. The stored
    /// <c>RunAtStartup</c> is only the first paint; the real state is read as soon as the row is up.
    /// </summary>
    private static void AddStartupRows(List<FrameworkElement> rows, AppHost host)
    {
        var advice = FluentLayout.Note(string.Empty);
        var detail = FluentLayout.Note(string.Empty);
        advice.Visibility = Visibility.Collapsed;
        detail.Visibility = Visibility.Collapsed;
        detail.Foreground = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);

        // Set while the switch is moved to match Windows, so that move is not taken as the user's.
        var adopting = false;
        ToggleSwitch? toggle = null;

        void Show(AutostartStatus status)
        {
            if (toggle is null)
            {
                return;
            }

            // An unreadable state keeps the switch where it was rather than claiming "off".
            if (status.State != AutostartState.Unknown)
            {
                adopting = true;
                toggle.IsOn = status.WillStart;
                adopting = false;
            }

            var text = AutostartRules.Advice(status.Problem)
                ?? (status.State == AutostartState.TurnedOffInWindows ? AutostartRules.Advice(AutostartProblem.TurnedOffInWindows) : null)
                ?? (status.IsLocked ? AutostartRules.Advice(AutostartProblem.Policy) : null);

            advice.Text = text ?? string.Empty;
            advice.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
            detail.Text = status.Detail is { Length: > 0 } raw && text is not null ? raw : string.Empty;
            detail.Visibility = detail.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

            if (host.State.Design.RunAtStartup != status.WillStart && status.State != AutostartState.Unknown)
            {
                host.Mutate(s => s.Design.RunAtStartup = status.WillStart, relayout: false);
            }
        }

        toggle = FluentLayout.Toggle(host.State.Design.RunAtStartup, async value =>
        {
            if (adopting)
            {
                return;
            }

            try
            {
                Show(await Autostart.SetAsync(value).ConfigureAwait(true));
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "changing the startup setting threw", ex);
            }
        });

        // [S-936] Read on every visit: the user may have switched Themia off in Task Manager since.
        toggle.Loaded += async (_, _) =>
        {
            try
            {
                Show(await Autostart.ReadAsync().ConfigureAwait(true));
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "reading the startup setting threw", ex);
            }
        };

        rows.Add(FluentLayout.Row(Glyphs.Startup, Str.Shell_Settings_General_Startup.Text(), Str.Shell_Settings_General_StartupDesc.Text(), toggle,
            "startup", "autostart", "sign in", "login", "boot"));
        rows.Add(advice);
        rows.Add(detail);
    }

    /// <summary>
    /// [S-930] Show or hide Windows' desktop icons. The switch adopts the live state when it comes on
    /// screen and whenever the icons are shown or hidden from anywhere — the desktop's own context
    /// menu included ([S-932]) — and only asks Windows when the user moves it ([S-931]). What it shows
    /// and caches is always what Windows reached, never the position asked for ([S-939]).
    /// </summary>
    private static Border DesktopIconsRow(AppHost host)
    {
        var adopting = false;
        ToggleSwitch? toggle = null;
        IDisposable? watch = null;

        void Adopt()
        {
            // While a request is being applied the list shows and hides on the way; the worker's
            // Settled reads the end state instead, so the switch never fights the user's hand.
            if (toggle is null || DesktopIcons.Busy)
            {
                return;
            }

            var live = DesktopIcons.AreVisible();
            var shown = DesktopIconRules.Shown(live, host.State.Preferences.DesktopIconsVisible);
            if (toggle.IsOn != shown)
            {
                adopting = true;
                toggle.IsOn = shown;
                adopting = false;
            }

            if (DesktopIconRules.Adopt(live, host.State.Preferences.DesktopIconsVisible) is bool cached)
            {
                host.Mutate(s => s.Preferences.DesktopIconsVisible = cached, relayout: false);
            }
        }

        void OnSettled()
        {
            // Raised on the worker thread; the switch belongs to the UI thread.
            if (toggle?.DispatcherQueue is not { } queue || !queue.TryEnqueue(Adopt))
            {
                Log.Debug("desktop-icons", "the settings page is gone; the switch reads the icons on the next visit");
            }
        }

        toggle = FluentLayout.Toggle(DesktopIconRules.Shown(DesktopIcons.AreVisible(), host.State.Preferences.DesktopIconsVisible), value =>
        {
            if (!adopting)
            {
                DesktopIcons.Request(value);
            }
        });

        // Listening only while on screen: the page is rebuilt on every visit and built off-screen by
        // search, so a hook installed for good would pile up. The hook's events arrive on this
        // thread's message loop, so the callback may touch the switch directly.
        toggle.Loaded += (_, _) =>
        {
            watch ??= DesktopIcons.Watch(Adopt);
            DesktopIcons.Settled += OnSettled;
            Adopt();
        };
        toggle.Unloaded += (_, _) =>
        {
            DesktopIcons.Settled -= OnSettled;
            watch?.Dispose();
            watch = null;
        };

        return FluentLayout.Row(Glyphs.Apps, Str.Settings_DesktopIcons_Label.Text(),
            Str.Shell_Settings_General_DesktopIconsDesc.Text(),
            toggle, "desktop icons", "hide icons", "clean desktop");
    }

    /// <summary>
    /// [S-920] Game mode's switch, with a live "Paused now" beside it while it is holding the widgets.
    /// </summary>
    private static Border GameModeRow(AppHost host)
    {
        var status = new TextBlock
        {
            Text = Str.Shell_Settings_General_PausedNow.Text(),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = FluentLayout.Themed("AccentTextFillColorPrimaryBrush", Colors.DodgerBlue),
            Visibility = host.Suspended ? Visibility.Visible : Visibility.Collapsed,
        };

        var toggle = FluentLayout.Toggle(host.State.Preferences.GameModeSuspend, host.SetGameModeSuspend);

        void OnSuspended(bool suspended) => status.Visibility = suspended ? Visibility.Visible : Visibility.Collapsed;
        toggle.Loaded += (_, _) =>
        {
            host.SuspendedChanged += OnSuspended;
            OnSuspended(host.Suspended);
        };
        toggle.Unloaded += (_, _) => host.SuspendedChanged -= OnSuspended;

        var control = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space3,
            Children = { status, toggle },
        };

        return FluentLayout.Row(Glyphs.Game, Str.Shell_Settings_General_GameMode.Text(),
            Str.Shell_Settings_General_GameModeDesc.Text(),
            control, "game mode", "fullscreen", "full screen", "presentation", "suspend", "battery");
    }
}
