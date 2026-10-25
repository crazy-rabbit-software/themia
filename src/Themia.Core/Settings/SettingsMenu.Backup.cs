using System.Text;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3575] What ⚙ › Back up &amp; reset does to the settings file: Windows' Save and Open dialogs over
/// the display whose menu it is, the file opened, the reset. How each ended is said under its line
/// (<see cref="ISettingsMenuHost.Outcomes"/>); an import is asked first beside the menu
/// (<see cref="SettingsMenu.ImportQuestion"/>), a reset by the menu itself.
/// </summary>
public interface IBackupMenuHost
{
    /// <summary>Where the settings file is (<c>state.json</c>).</summary>
    string StatePath { get; }

    /// <summary>[S-494] Save the settings to a file picked in Windows' Save dialog.</summary>
    void Export();

    /// <summary>[S-495]..[S-497] Pick a file, read it, ask what it changes, and only on Import take it.</summary>
    void Import();

    /// <summary>[S-3578] Open the settings file in what edits JSON here (its folder before it is written).</summary>
    void OpenFile();

    /// <summary>[S-892] Back to a fresh profile, the settings there are now kept beside the file. Already asked.</summary>
    void Reset();
}

/// <summary>
/// [S-3575]..[S-3578] Back up &amp; reset ›: the About page's settings file rows as lines, after Karpo's
/// ⚙ › Back up and reset (karpo-app, docs/widgets.md §339): Export settings…, Import settings…, Open
/// settings file (its path under it), then Reset all settings…, asked first.
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>[S-3577] How many lines of each part of an import's question are listed before "and N more".</summary>
    public const int PreviewLines = 12;

    /// <summary>[S-3575] The Back up &amp; reset line and its submenu.</summary>
    public static SettingsSubmenu Backup(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var backup = host.Backup;
        var outcomes = host.Outcomes;
        var stateName = Path.GetFileName(backup.StatePath);

        // [S-3576] While Windows' dialog is up, nothing else of the file is started.
        var busy = outcomes.Busy(Keys.BackupExport) || outcomes.Busy(Keys.BackupImport) || outcomes.Busy(Keys.BackupReset);

        var lines = new List<SettingsLine>
        {
            new SettingsCommand(Keys.BackupExport, Str.SettingsMenu_ExportSettings.Text(), Glyphs.Export, backup.Export) { Disabled = busy },
            OutcomeNote(outcomes, Keys.BackupExport),
            new SettingsCommand(Keys.BackupImport, Str.SettingsMenu_ImportSettings.Text(), Glyphs.Import, backup.Import) { Disabled = busy },
            OutcomeNote(outcomes, Keys.BackupImport),
            new SettingsCommand(Keys.BackupOpen, Str.Shell_About_OpenConfig.Text(), Glyphs.Code, backup.OpenFile),
            new SettingsNote(Keys.BackupOpen + "/path", backup.StatePath),
            new SettingsSeparator(Keys.Backup + "/-reset"),

            // [S-892] Asked first, as Windows asks before it destroys something: the page's words.
            new SettingsCommand(Keys.BackupReset, Str.SettingsMenu_ResetAll.Text(), Glyphs.Reset, () => host.Confirm(new SettingsConfirm(
                Str.Shell_About_ResetConfirm.Text(),
                Str.SettingsMenu_Reset.Text(),
                backup.Reset,
                Str.Shell_About_ResetBody.Format(("file", stateName)),
                Danger: true)))
            {
                Danger = true,
                Disabled = busy,
            },
            OutcomeNote(outcomes, Keys.BackupReset),
        };

        return new SettingsSubmenu(Keys.Backup, Str.SettingsMenu_Backup.Text(), Glyphs.History, lines);
    }

    /// <summary>
    /// [S-3577] [S-496] The question an import asks beside the menu before anything changes: what
    /// changes, a line each ("Nothing" when the file holds what is there now), then what is good to
    /// know (repairs, displays not here, what stays this PC's, where the current settings are kept),
    /// each part cut at <see cref="PreviewLines"/> with "and N more". Import does it; Cancel, Escape
    /// or a click away leave everything as it was.
    /// </summary>
    public static SettingsConfirm ImportQuestion(ImportPreview preview, string stateName, string backupName, Action import)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(import);

        var detail = new StringBuilder();
        Part(detail, Str.Shell_About_WhatChanges.Text(), preview.Changes.Count > 0 ? preview.Changes : [Str.Shell_About_NoChanges.Text()]);
        detail.Append('\n');
        Part(detail, Str.Shell_About_GoodToKnow.Text(), [.. preview.Notes, Str.Shell_About_KeptAs.Format(("file", stateName), ("backup", backupName))]);

        return new SettingsConfirm(Str.Shell_About_ImportConfirm.Text(), Str.Common_Import.Text(), import, detail.ToString().TrimEnd('\n'));
    }

    /// <summary>One part of an import's question: its heading, a line each, and how many more there are.</summary>
    private static void Part(StringBuilder detail, string heading, IReadOnlyList<string> lines)
    {
        detail.Append(heading).Append('\n');
        foreach (var line in lines.Take(PreviewLines))
        {
            detail.Append("• ").Append(line).Append('\n');
        }

        if (lines.Count > PreviewLines)
        {
            detail.Append(Str.Shell_About_AndMore.Plural(lines.Count - PreviewLines)).Append('\n');
        }
    }
}
