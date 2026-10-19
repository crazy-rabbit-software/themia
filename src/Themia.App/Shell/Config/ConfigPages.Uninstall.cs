using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Storage;

namespace Themia.App.Shell.Config;

/// <summary>
/// "Uninstall Themia" at the foot of the About page. [S-1552..S-1555]
/// <para>
/// A danger card, last on the page. Its button asks first, in a dialog that says what will happen
/// and offers — unticked — to also delete the user's widgets, settings and accounts. Nothing is
/// removed or deleted before that dialog's Uninstall is pressed. Then: the goodbye survey opens (as
/// the original's installer opened it on an interactive uninstall), Themia closes and saves, and
/// only after that — so the final save cannot write the folders back — the data goes (when asked)
/// and the app itself: a Store/MSIX install removes its own package, a loose build opens Windows'
/// Installed apps for the user to finish.
/// </para>
/// </summary>
internal static partial class ConfigPages
{
    /// <summary>APPMODEL_ERROR_NO_PACKAGE: the process has no package identity.</summary>
    private const int NoPackage = 15700;

    /// <summary>ERROR_INSUFFICIENT_BUFFER: the call that sizes the buffer.</summary>
    private const int InsufficientBuffer = 122;

    private static Border UninstallRow(AppHost host)
    {
        var button = new Button { Content = Str.Shell_Uninstall_Action.Text() };
        button.Click += (_, _) => _ = UninstallAsync(host, button);

        return FluentLayout.Row(Glyphs.Delete, Str.Shell_Uninstall_Title.Text(),
            Str.Shell_Uninstall_Desc.Text(), button);
    }

    private static async Task UninstallAsync(AppHost host, Button source)
    {
#if DEBUG
        if (Gallery.SettingsRender.IsRendering)
        {
            Log.Warn(Scope, "uninstall pressed in a settings render; nothing was done");
            return;
        }
#endif

        var packageName = PackageFullName();
        var preview = UninstallPlan.Create(
            packaged: packageName is not null,
            deleteData: true,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppStateStore.FolderName,
            AppInfo.Version);

        if (await AskUninstallAsync(source, preview) is not { } deleteData)
        {
            return;
        }

        // [S-1070] Packaged, the data goes with the package whatever the box says (there is none).
        var plan = deleteData || preview.DataGoesWithPackage ? preview : preview with { FoldersToDelete = [] };
        Log.Info(Scope, $"uninstalling ({plan.Route}, delete data: {deleteData})");

        // The survey first, while there is still a process to open it.
        Launch(plan.Survey);

        host.RequestExit(() => FinishUninstallAsync(plan, packageName));
    }

    /// <summary>
    /// The confirmation. Null for cancel; otherwise whether the data goes too (unticked by default).
    /// </summary>
    private static async Task<bool?> AskUninstallAsync(FrameworkElement source, UninstallPlan plan)
    {
        if (source.XamlRoot is not { } root)
        {
            Log.Warn(Scope, "no XamlRoot for the uninstall dialog; nothing was done");
            return null;
        }

        var what = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = plan.Route == UninstallRoute.RemovePackage
                ? Str.Shell_Uninstall_Package.Text()
                : Str.Shell_Uninstall_InstalledApps.Text(),
        };

        var body = new StackPanel { Spacing = Fluent.Space3, MaxWidth = 440, Children = { what } };

        var deleteData = new CheckBox { Content = Str.Shell_Uninstall_DeleteData.Text() };

        var where = new TextBlock { Text = string.Join(Environment.NewLine, plan.FoldersToDelete) };
        FluentLayout.ApplyStyle(where, "SettingsCaptionTextBlockStyle");
        where.Margin = new Thickness(28, 0, 0, 0);
        where.Visibility = Visibility.Collapsed;

        // The folders are named once the box is ticked, so the user sees exactly what goes.
        deleteData.Checked += (_, _) => where.Visibility = Visibility.Visible;
        deleteData.Unchecked += (_, _) => where.Visibility = Visibility.Collapsed;

        // [S-1070] Packaged, Windows removes the package's data with it, so keeping it is not a
        // choice the dialog can honestly offer.
        if (!plan.DataGoesWithPackage)
        {
            body.Children.Add(deleteData);
            body.Children.Add(where);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            RequestedTheme = source.ActualTheme,
            Title = Str.Shell_Uninstall_Confirm.Text(),
            Content = body,
            PrimaryButtonText = Str.Shell_Uninstall_Action.Text(),
            CloseButtonText = Str.Common_Cancel.Text(),
            DefaultButton = ContentDialogButton.Close,
        };

        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary ? deleteData.IsChecked == true : null;
        }
        catch (Exception ex)
        {
            // No dialog, no uninstall: refusing beats removing something unasked.
            Log.Error(Scope, "could not show the uninstall confirmation", ex);
            return null;
        }
    }

    /// <summary>
    /// [S-1071] After the app has closed and saved: <see cref="UninstallPlan.Steps"/>, in order —
    /// autostart off, then the data, then the app itself. A failing step is logged and the next one
    /// still runs, except that a failed package removal falls back to the app's page in Settings.
    /// </summary>
    private static async Task FinishUninstallAsync(UninstallPlan plan, string? packageName)
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
                    Launch(plan.Fallback);
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
            Launch(plan.Fallback);
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
            Launch(plan.Fallback);
        }
    }

    private static void Launch(Uri uri)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Error(Scope, $"could not open {uri}", ex);
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
