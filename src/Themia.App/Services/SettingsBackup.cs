using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Storage;

namespace Themia.App.Services;

/// <summary>
/// [S-3575]..[S-3578] What ⚙ › Back up &amp; reset does to the settings file, the About page's flows run
/// from the menu's lines: Windows' Save and Open dialogs (<c>Microsoft.Windows.Storage.Pickers</c>,
/// which take a window id and work unpackaged) owned by the window of the display whose ⚙ it is,
/// the import's question asked beside that menu, the file opened through the one launcher, the reset.
/// How each ended is said under its line; a dialog cancelled says nothing.
/// </summary>
internal sealed class SettingsBackup(AppHost host, SettingsOutcomes outcomes)
{
    private const string Scope = "settings-file";

    /// <summary>[S-494] Save the whole state to a file picked in Windows' Save dialog, owned by <paramref name="owner"/>.</summary>
    public async void Export(WindowId owner)
    {
        if (Busy)
        {
            return;
        }

        const string row = SettingsMenu.Keys.BackupExport;
        outcomes.Say(row, new MenuOutcome(string.Empty, Busy: true));
        try
        {
            var picker = new FileSavePicker(owner)
            {
                SuggestedFileName = Str.Shell_About_ExportFileName.Format(("date", DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))),
                DefaultFileExtension = ".json",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeChoices.Add(Str.Shell_About_FileType.Text(), [".json"]);

            if (await picker.PickSaveFileAsync() is not { } picked)
            {
                outcomes.Say(row, null); // ignore: the user cancelled the dialog
                return;
            }

            if (SettingsTransfer.WriteFile(host.State, picked.Path) is { } problem)
            {
                outcomes.Say(row, new MenuOutcome(problem, Failed: true));
                return;
            }

            Log.Info(Scope, $"exported settings to {picked.Path}");
            outcomes.Say(row, new MenuOutcome(Str.SettingsMenu_Exported.Format(("file", Path.GetFileName(picked.Path)))));
        }
        catch (Exception ex)
        {
            // An async void: nothing may escape it. COMException is the usual one: the dialog could not be shown.
            Log.Error(Scope, "export failed", ex);
            outcomes.Say(row, new MenuOutcome(Str.Shell_About_ExportFailed.Text(), Failed: true));
        }
    }

    /// <summary>
    /// [S-495]..[S-497] Pick a file in Windows' Open dialog, read it through the store's own
    /// validation, ask beside the menu what it would change (<see cref="SettingsMenu.ImportQuestion"/>,
    /// through <paramref name="confirm"/>), and only on Import keep the current settings beside the
    /// state file and take it.
    /// </summary>
    public async void Import(WindowId owner, Action<SettingsConfirm> confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        if (Busy)
        {
            return;
        }

        const string row = SettingsMenu.Keys.BackupImport;
        outcomes.Say(row, new MenuOutcome(string.Empty, Busy: true));
        try
        {
            var picker = new FileOpenPicker(owner) { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".json");

            if (await picker.PickSingleFileAsync() is not { } picked)
            {
                outcomes.Say(row, null); // ignore: the user cancelled the dialog
                return;
            }

            var read = SettingsTransfer.ReadFile(picked.Path);
            if (read.State is not { } incoming)
            {
                outcomes.Say(row, new MenuOutcome(read.Problem ?? Str.Shell_Transfer_Unreadable.Text(), Failed: true));
                return;
            }

            var displays = host.Displays.Select(d => d.Id).ToList();
            var preview = SettingsTransfer.Compare(host.State, incoming, read.Repairs, displays);
            var stateName = Path.GetFileName(host.StatePath);
            var backupName = Path.GetFileName(SettingsTransfer.BackupPath(host.StatePath, DateTime.Now));

            // Asked, not yet done: the line is free again while the question stands.
            outcomes.Say(row, null);
            confirm(SettingsMenu.ImportQuestion(preview, stateName, backupName, () =>
            {
                if (host.ImportState(incoming, out var backup) is { } problem)
                {
                    outcomes.Say(row, new MenuOutcome(problem, Failed: true));
                    return;
                }

                outcomes.Say(row, new MenuOutcome(Str.SettingsMenu_Imported.Format(("backup", Path.GetFileName(backup)))));
            }));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "import failed", ex);
            outcomes.Say(row, new MenuOutcome(Str.Shell_About_ImportFailed.Text(), Failed: true));
        }
    }

    /// <summary>[S-3578] Open the state file in whatever edits JSON here; its folder when it has not been written yet.</summary>
    public void OpenFile()
    {
        var path = host.StatePath;
        var target = File.Exists(path) ? path : Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(target))
        {
            Log.Warn(Scope, "the settings file has no folder to open");
            return;
        }

        if (ShellLauncher.Open(target) != LaunchOutcome.Opened)
        {
            Log.Warn(Scope, $"could not open {target}");
        }
    }

    /// <summary>[S-892] Back to a fresh profile, what is there now kept beside the settings file. Already asked.</summary>
    public void Reset()
    {
        const string row = SettingsMenu.Keys.BackupReset;
        if (host.ResetAll(out var backup) is { } problem)
        {
            outcomes.Say(row, new MenuOutcome(problem, Failed: true));
            return;
        }

        outcomes.Say(row, new MenuOutcome(Str.SettingsMenu_ResetDone.Format(("backup", Path.GetFileName(backup)))));
    }

    /// <summary>[S-3576] One of Windows' dialogs is up: nothing else of the file is started.</summary>
    private bool Busy => outcomes.Busy(SettingsMenu.Keys.BackupExport) || outcomes.Busy(SettingsMenu.Keys.BackupImport);
}
