using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Storage;
using Themia.Sdk.WinUI;

namespace Themia.App;

public partial class App : Application
{
    private const string Scope = "app";

    private AppHost? _host;

    public App()
    {
        // [S-6902] The widget registry, before anything reads a widget.
        Services.ModuleHost.Install();

        // [S-822] The design engine behind every part's theme brushes, before anything is drawn.
        Themia.Sdk.WinUI.ThemeBrush.Install(Shell.DesignApplier.Brush);
        Widgets.ItemFill.Install(new Widgets.ItemInks.Engine());
        Widgets.ItemMenu.InstallFileIcons((file, pixels) => Services.Files.FileIcons.IconAsync(new Themia.Core.Widgets.FolderEntry(Path.GetFileName(file), file, false, 0, default, false), pixels));
        Shell.ColorChoice.InstallEyedropper(new Interop.ScreenEyedropper());
        Shell.Config.PlaceSearch.InstallLookup(Services.Web.WeatherService.FindAsync);
        Themia.Sdk.SymbolLookup.Install(Services.Web.MarketService.SearchAsync);
        Themia.Sdk.DriveList.Install(() => Services.Drives.DriveService.Shared.Now());
        Themia.Sdk.AudioDevices.Install(Services.CoreAudio.OutputDevicesAsync);
        Themia.Sdk.GpuAdapters.Install(Services.GpuSampler.Adapters);
        Themia.Sdk.ProcessPaths.Install(Services.ProcessNames.ExePath, Services.ProcessNames.DisplayName);
        Themia.Sdk.WinUI.AppIconSource.Install(Services.AppIcons.GetAsync, Services.AppIcons.DefaultSize);
        Themia.Sdk.WinUI.SettingsHost.Install(() => Services.AppServices.Notes, () => Services.AppServices.Habits, () => Services.AppServices.ScreenTime, Services.HostLauncher.Instance,
            picker => WinRT.Interop.InitializeWithWindow.Initialize(picker, Interop.Win32.GetForegroundWindow()), Services.HostAppLayouts.Instance);
        Widgets.GlideMotion.InstallSmooth(windows => Themia.Core.Motion.GlideSwitch.Smooth(Shell.DesignApplier.Current, windows));
        Services.SoundCue.Install(new Services.HostCuePlayer());

#if DEBUG
        // The settings render's --theme: the application theme can only be chosen before the
        // resources load, and the pages' brushes come from it.
        if ((Shell.Gallery.SettingsRender.RequestedTheme(Environment.GetCommandLineArgs())
            ?? Shell.Gallery.IconPickerRender.RequestedTheme(Environment.GetCommandLineArgs())
            ?? Shell.Gallery.EditBarRender.RequestedTheme(Environment.GetCommandLineArgs())
            ?? Shell.Gallery.WidgetRender.RequestedTheme(Environment.GetCommandLineArgs())) is { } theme)
        {
            RequestedTheme = theme;
        }
#endif

        InitializeComponent();

        // [S-987] Only an explicit quit ends the process. WinUI's default ends it when the last
        // window closes, and Themia has no main window: the display pass closes a surface before it
        // builds the replacement, so re-keying a lone display, rebuilding a dead surface or every
        // panel powering off at once exited silently (the original's #87). Every way out calls Exit.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

        UnhandledException += OnUnhandledException;

        // [S-988] A throw on a background thread ends the process whatever is done here; it is put on
        // record first (the log flushes per record), so a crash is never "without a trace".
        AppDomain.CurrentDomain.UnhandledException += OnFatalException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // [S-1553] The uninstall's data cleanup: waits for the app to exit, deletes its folders and
        // quits. Before the log sink, which would write a log file back into a folder being deleted.
        var commandLine = Environment.GetCommandLineArgs();
        if (Array.IndexOf(commandLine, DataPurge.Flag) >= 0)
        {
            var queue = DispatcherQueue.GetForCurrentThread();
            _ = Task.Run(() =>
            {
                try
                {
                    DataPurge.TryRunFromCommandLine(commandLine);
                }
                finally
                {
                    if (queue is null || !queue.TryEnqueue(Exit))
                    {
                        Environment.Exit(0);
                    }
                }
            });
            return;
        }

        // The debugger only: the debug renders below must
        // never write into the user's log folder while the user's own copy is running ([S-980]).
        LogFiles.InstallBasics();

#if DEBUG
        // [S-1814] Showcase views into a state file, then exit. Before the single-instance hand-off,
        // which would toggle the running copy's edit mode instead.
        if (Shell.Gallery.ShowcaseCommand.TryRun(Environment.GetCommandLineArgs()) is { } exitCode)
        {
            Environment.Exit(exitCode);
            return;
        }

        // [S-1109] --lang <code> draws any render below in that language.
        Shell.Gallery.RenderLanguage.Apply(Environment.GetCommandLineArgs());

        // Each render runs instead of the app, never alongside it: no state file, no single-instance
        // hand-off, so it can draw while the user's own copy keeps running. One settings page is
        // built over a demo state that is never saved.
        if (Shell.Gallery.SettingsRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And for the edit bar's open add menu ([S-3506]).
        if (Shell.Gallery.PickerRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And for the edit bar ([S-3506]).
        if (Shell.Gallery.EditBarRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And for the open icon picker ([S-1803]).
        if (Shell.Gallery.IconPickerRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And for the desktop view bar ([S-906]) at its anchors and sizes.
        if (Shell.Gallery.ViewBarRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And for the desktop's notices and a widget's error card ([S-880], [S-891]).
        if (Shell.Gallery.NoticeRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And for the app-layout editor ([S-1411]).
        if (Shell.Gallery.LayoutEditorRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And for one widget at every size tier, or one size ([S-1917]).
        if (Shell.Gallery.WidgetRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And for the context menus ([S-1740]..[S-1744]).
        if (Shell.Gallery.MenuRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And for the intros, frozen mid-flight ([S-1016]).
        if (Shell.Gallery.MotionRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And the edit-mode snap grid ([S-06c]).
        if (Shell.Gallery.GridRender.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And what a view switch costs, and whether it leaks ([S-1883]).
        if (Shell.Gallery.SwitchCheck.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }

        // And the surface's drop plumbing, exercised off-screen ([S-1026]).
        if (Shell.Gallery.DropCheck.TryRunFromCommandLine(this, Environment.GetCommandLineArgs()))
        {
            return;
        }
#endif

        // Themia has no tray entry and no window in the taskbar, so launching it again is the
        // gesture a user reaches for when they want at it. That has to mean "show me the copy I
        // already have", not "start a second desktop layer on top of it" — there is nothing on
        // screen that would tell the two apart.
        if (CommandWindow.TrySignalRunningInstance())
        {
            Log.Info(Scope, "already running; handed the request to that copy");
            Exit();
            return;
        }

        // After the hand-off, so a second copy that is only passing the request on never writes into
        // the running copy's log file; before the load, so a corrupt state file is on record.
        LogFiles.InstallFiles();

        // [S-934] A sign-in launch (the Run entry's --autostart, or the package's startup task) and
        // a launch by hand are told apart here, once, for whatever wants to stay quiet at sign-in.
        Autostart.DetectLaunch(commandLine);

        var store = new AppStateStore(AppStateStore.DefaultPath());
        var load = store.Load();

        if (load.Outcome == LoadOutcome.Recovered)
        {
            // Defaults are showing and the user's layout is not. That is worth saying out loud;
            // the previous file is preserved next to the state file.
            Log.Warn(Scope, $"started with default layout: {load.Detail}");
        }

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        if (dispatcher is null)
        {
            Log.Error(Scope, "no dispatcher queue on the launch thread; cannot start");
            Exit();
            return;
        }

        _host = new AppHost(dispatcher, store, load.State);
        _host.Start();
    }

    private static void OnFatalException(object sender, System.UnhandledExceptionEventArgs e)
    {
        try
        {
            Log.Error(Scope, $"unhandled exception off the UI thread (terminating: {e.IsTerminating})", e.ExceptionObject as Exception);
        }
        catch (Exception)
        {
            // ignore: the process is going down and the logger itself failed; nothing is left to tell.
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(Scope, "a background task failed and nobody awaited it", e.Exception);
        e.SetObserved();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Error(Scope, "unhandled exception on the UI thread", e.Exception);

        // Keep the widget layer alive. A throw while painting one widget should not take the whole
        // desktop down; the error is recorded and the surface carries on.
        e.Handled = true;
    }
}
