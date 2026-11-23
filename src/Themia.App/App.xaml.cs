using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;
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
        // A Windows location (shell:) is no file: its icon is its own, not the one every extensionless
        // file shares, which drew This PC with the Recycle Bin's.
        // [S-5559] A kind of file named without a file (New ›'s lines) is its registered type's icon.
        Widgets.ItemMenu.InstallFileIcons((file, pixels) => file.StartsWith(Services.Files.TypeIconName.Prefix, StringComparison.Ordinal)
            ? Services.Files.FileIcons.TypeIconAsync(file, pixels)
            : file.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
            ? Themia.Sdk.WinUI.AppIconSource.GetAsync(file, pixels, Current.RequestedTheme == ApplicationTheme.Dark ? Themia.Core.Widgets.LogoTheme.Dark : Themia.Core.Widgets.LogoTheme.Light)
            : Services.Files.FileIcons.IconAsync(new Themia.Core.Widgets.FolderEntry(Path.GetFileName(file), file, Directory.Exists(file), 0, default, false), pixels));
        // [S-5565] The number in the badge of a drag's ghost: real text, drawn at once.
        Themia.Sdk.WinUI.ItemDrag.InstallInk(Interop.TextInk.Draw);
        // [S-14200] Where a text's ink really stands, for the faces that stand on it, before anything is drawn.
        Themia.Sdk.Kit.InkMeter.Install(Interop.InkMeasure.Measure);
        // [S-13025] A drag of ours starts from the foreground window, or OLE's loop sees no mouse (the first drag after another app).
        Themia.Sdk.WinUI.ItemDrag.InstallForeground(window => _host?.TakeForegroundForDrag(window) ?? true);
        Shell.ColorChoice.InstallEyedropper(new Interop.ScreenEyedropper());

        // [S-1575] The picker's recent colours live in the state (none before the host is there: a render).
        Shell.ColorChoice.InstallRecent(
            () => _host?.State.Preferences.RecentColours ?? [],
            hex => _host?.Mutate(s => s.Preferences.RecentColours = [.. Themia.Core.Settings.ColourPicker.Remember(s.Preferences.RecentColours, hex)], relayout: false, undo: null));
        Shell.Config.PlaceSearch.InstallLookup(Services.Web.WeatherService.FindAsync);
        Themia.Sdk.SymbolLookup.Install(Services.Web.MarketService.SearchAsync);
        Themia.Sdk.DriveList.Install(() => Services.Drives.DriveService.Shared.Now());
        Themia.Sdk.AudioDevices.Install(Services.CoreAudio.OutputDevicesAsync);
        Themia.Sdk.Devices.BluetoothList.Install(() => Services.Devices.BluetoothDeviceService.Shared.Last);
        Themia.Sdk.GpuAdapters.Install(Services.GpuSampler.Adapters);
        // [S-14961] The machine the hub reads (the sample PC's under a render's --machine sample), this PC's without a hub.
        Themia.Sdk.AdapterList.Install(() => Services.AppServices.System?.Adapters() ?? Services.NetworkAdapterWalk.Now(), Services.NetworkAdapterWalk.Watch);
        Themia.Sdk.BrightnessDisplays.Install(() => Services.Brightness.BrightnessService.Snapshot);
        Themia.Sdk.ProcessPaths.Install(Services.ProcessNames.ExePath, Services.ProcessNames.DisplayName);
        Themia.Sdk.ProcessControl.Install(Services.ProcessEnding.End);
        Themia.Sdk.WinUI.AppIconSource.Install(Services.AppIcons.GetAsync, Services.AppIcons.DefaultSize);
        Themia.Sdk.WinUI.SettingsHost.Install(() => Services.AppServices.Notes, () => Services.AppServices.Habits, () => Services.AppServices.ScreenTime, Services.HostLauncher.Instance,
            picker => WinRT.Interop.InitializeWithWindow.Initialize(picker, Interop.Win32.GetForegroundWindow()), Services.HostAppLayouts.Instance,
            () => Services.Office.OfficeSources.Current, () => Services.AppServices.Tasks,
            feature => Services.AppServices.Accounts?.WithFeature(feature) ?? []);
        Services.SoundCue.Install(new Services.HostCuePlayer());

#if DEBUG
        // [S-69] A render's --theme: the application theme can only be chosen before the resources load,
        // and the brushes come from it.
        if (Shell.RenderCommands.ThemeOf(Environment.GetCommandLineArgs()) is { } theme && theme != Core.Model.AppTheme.System)
        {
            RequestedTheme = theme == Core.Model.AppTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark;
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
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
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

        // [S-68] A flag this build does not know never starts the app and never reaches the hand-off,
        // which would toggle the running copy's edit mode: it is named and the process exits.
        var start = StartArguments.Classify(commandLine, RenderCommandLine.Flags);
        if (start.Request == StartRequest.Unknown)
        {
            RefuseStart(StartArguments.UnknownFlagMessage(start.Flag ?? string.Empty));
            return;
        }

        // [S-15872] [S-15873] A render or check takes no part in the desktop the user's own copy is on, and Show Desktop does not see its windows.
        if (start.Request == StartRequest.Render)
        {
            DesktopZOrder.StandAside();
            OffDesktop.Enter();
        }

#if DEBUG
        // [S-1109] --lang <code> draws any render in that language.
        Shell.Gallery.RenderLanguage.Apply(commandLine);
#endif

        // [S-69] A render or check runs instead of the app, never alongside it: no state file, no single-instance
        // hand-off, so it can draw while the user's own copy keeps running, over a demo state that is never saved.
        // [S-11854] A Release build has the add page's check alone.
        if (Shell.RenderCommands.TryRun(this, commandLine))
        {
            return;
        }

        // [S-934] A sign-in launch (the Run entry's --autostart, or the package's startup task) and
        // a launch by hand are told apart here, once, for whatever wants to stay quiet at sign-in;
        // before the hand-off, which tells the running copy which of the two this was ([S-66]).
        Autostart.DetectLaunch(commandLine);

        // Themia has no tray entry and no window in the taskbar, so launching it again is the
        // gesture a user reaches for when they want at it. That has to mean "show me the copy I
        // already have" (edit mode with its settings menu open, [S-66]), not "start a second
        // desktop layer on top of it" — there is nothing on screen that would tell the two apart.
        if (CommandWindow.TrySignalRunningInstance(Autostart.LaunchSource))
        {
            Log.Info(Scope, "already running; handed the request to that copy");
            Exit();
            return;
        }

        // After the hand-off, so a second copy that is only passing the request on never writes into
        // the running copy's log file; before the load, so a corrupt state file is on record.
        LogFiles.InstallFiles();
        Log.Info(Scope, Autostart.LaunchSource == LaunchSource.Autostart ? "started at sign-in" : "started by hand");

        // [S-6612] Under memory pressure, Themia's pages go before the user's apps'.
        MemoryPriority.Lower();

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

    /// <summary>[S-68] Say why nothing started, on the console and in the log, and exit non-zero.</summary>
    private static void RefuseStart(string message)
    {
        Log.Error(Scope, message);
        Console.Error.WriteLine(message);
        Environment.Exit(StartArguments.UnknownFlagExitCode);
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

        // [S-5504] Last chance: Explorer's icons back if this run hid them (never throws, never blocks).
        Interop.DesktopIcons.RestoreIfHidden();
    }

    /// <summary>[S-5504] The process is exiting, however it got there: Explorer's icons back if this run hid them.</summary>
    private static void OnProcessExit(object? sender, EventArgs e) => Interop.DesktopIcons.RestoreIfHidden();

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
