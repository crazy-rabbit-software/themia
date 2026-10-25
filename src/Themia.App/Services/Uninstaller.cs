using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Settings;
using Themia.Core.Storage;

namespace Themia.App.Services;

/// <summary>
/// [S-1552]..[S-1555] Themia taking itself off the PC, once ⚙ › About Themia › Uninstall Themia ›
/// Uninstall… has been answered ([S-3582]): the goodbye survey opens (as the original's installer
/// opened it on an interactive uninstall), Themia closes and saves, and only after that, so the
/// final save cannot write the folders back, the data goes (when asked) and the app itself: a
/// Store/MSIX install removes its own package, a loose build opens Windows' Installed apps for the
/// user to finish. Nothing is removed or deleted before that answer.
/// </summary>
internal static partial class Uninstaller
{
    private const string Scope = "uninstall";

    /// <summary>APPMODEL_ERROR_NO_PACKAGE: the process has no package identity.</summary>
    private const int NoPackage = 15700;

    /// <summary>ERROR_INSUFFICIENT_BUFFER: the call that sizes the buffer.</summary>
    private const int InsufficientBuffer = 122;

    /// <summary>This process's package full name, read once: null when it runs without package identity.</summary>
    private static readonly Lazy<string?> Package = new(PackageFullName);

    /// <summary>
    /// [S-1552] [S-1553] What uninstalling would do here, the data folders named as if they went (what
    /// the menu shows once "Also delete" is ticked; packaged, they always go, [S-1070]).
    /// </summary>
    public static UninstallPlan Preview { get; } = UninstallPlan.Create(
        packaged: Package.Value is not null,
        deleteData: true,
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppStateStore.FolderName,
        AppInfo.Version);

    /// <summary>[S-1552] Uninstall, the data too where <paramref name="deleteData"/> (packaged: always). Already asked.</summary>
    public static void Run(AppHost host, bool deleteData)
    {
        ArgumentNullException.ThrowIfNull(host);

        // [S-1070] Packaged, the data goes with the package whatever was ticked (nothing was offered).
        var plan = deleteData || Preview.DataGoesWithPackage ? Preview : Preview with { FoldersToDelete = [] };
        Log.Info(Scope, $"uninstalling ({plan.Route}, delete data: {plan.FoldersToDelete.Count > 0})");

        // [S-1554] The survey first, while there is still a process to open it.
        Open(plan.Survey);

        var packageName = Package.Value;
        host.RequestExit(() => FinishAsync(plan, packageName));
    }

    /// <summary>
    /// [S-1071] After the app has closed and saved: <see cref="UninstallPlan.Steps"/>, in order —
    /// autostart off, then the data, then the app itself. A failing step is logged and the next one
    /// still runs, except that a failed package removal falls back to the app's page in Settings.
    /// </summary>
    private static async Task FinishAsync(UninstallPlan plan, string? packageName)
    {
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case UninstallStep.RemoveAutostart:
                    // Never throws; a refusal comes back as a status with a problem.
                    var autostart = await Autostart.SetAsync(false).ConfigureAwait(true);
                    if (autostart.Problem != Themia.Core.Shell.AutostartProblem.None)
                    {
                        Log.Warn(Scope, $"could not switch autostart off while uninstalling ({autostart.Problem})");
                    }

                    break;

                case UninstallStep.DeleteDataNow:
                    // Packaged: removing the package ends this process, so the data goes first, from here.
                    foreach (var folder in plan.FoldersToDelete)
                    {
                        DataPurge.Delete(folder);
                    }

                    break;

                case UninstallStep.RemovePackage:
                    await RemoveOwnPackageAsync(plan, packageName).ConfigureAwait(true);
                    break;

                case UninstallStep.StartDataPurge:
                    // Loose build: the folders go once this process (and its browser) has exited.
                    if (!DataPurge.Start())
                    {
                        Log.Warn(Scope, "could not start the data cleanup; the data folders were left in place");
                    }

                    break;

                case UninstallStep.OpenInstalledApps:
                    Open(plan.Fallback);
                    break;
            }
        }
    }

    /// <summary>
    /// [S-1552] <c>PackageManager.RemovePackageAsync</c> on this process's own package (per user, the
    /// way the Store installed it). No capability: a full-trust app runs at medium integrity and
    /// may manage its own user's packages. Windows ends this process while it removes the package.
    /// </summary>
    private static async Task RemoveOwnPackageAsync(UninstallPlan plan, string? packageName)
    {
        if (packageName is null)
        {
            Log.Error(Scope, "packaged uninstall without a package name; opening the app's page in Settings");
            Open(plan.Fallback);
            return;
        }

        try
        {
            var result = await new Windows.Management.Deployment.PackageManager().RemovePackageAsync(packageName);
            if (result.ExtendedErrorCode is { } error)
            {
                throw error;
            }
        }
        catch (Exception ex)
        {
            // Removal refused (policy, a pending update): Settings' page for the app has the
            // same Uninstall button.
            Log.Error(Scope, "could not remove the package; opening its page in Settings", ex);
            Open(plan.Fallback);
        }
    }

    /// <summary>The survey through the launcher's web rule, Windows' Installed apps through its settings rule.</summary>
    private static void Open(Uri uri)
    {
        var outcome = uri.Scheme == "ms-settings" ? ShellLauncher.OpenWindowsSettings(uri) : ShellLauncher.Open(uri.AbsoluteUri);
        if (outcome != LaunchOutcome.Opened)
        {
            Log.Warn(Scope, $"could not open {uri} ({outcome})");
        }
    }

    /// <summary>This process's package full name, or null when it runs without package identity.</summary>
    private static unsafe string? PackageFullName()
    {
        uint length = 0;
        var status = GetCurrentPackageFullName(ref length, null);

        if (status == NoPackage)
        {
            return null;
        }

        if (status != InsufficientBuffer || length == 0)
        {
            Log.Warn(Scope, $"could not tell whether Themia is packaged (error {status}); treating it as not");
            return null;
        }

        var buffer = new char[length];
        fixed (char* chars = buffer)
        {
            status = GetCurrentPackageFullName(ref length, chars);
        }

        return status == 0 ? new string(buffer, 0, (int)Math.Max(0, length - 1)) : null;
    }

    [LibraryImport("kernel32.dll")]
    private static unsafe partial int GetCurrentPackageFullName(ref uint length, char* name);
}
