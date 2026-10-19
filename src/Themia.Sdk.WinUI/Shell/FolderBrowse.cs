using Themia.Core.Diagnostics;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// The shell's own folder picker behind the properties menu's folder lines (the folder widget's, the
/// gallery's, a shortcut's), so nobody has to type a path they could point at.
/// <para>
/// The picker needs an owner window (<see cref="SettingsHost.OwnPicker"/>).
/// </para>
/// </summary>
public static class FolderBrowse
{
    private const string Scope = "folder-settings";

    /// <summary>
    /// [S-3730] The picker on its own: <paramref name="chosen"/> gets the folder picked, and nothing
    /// happens on a cancel. The properties menu's folder line ([S-3730]) and its pickers go through
    /// it, so there is one picker.
    /// </summary>
    public static async Task PickAsync(Action<string> chosen)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();

            // Required: PickSingleFolderAsync throws without at least one filter.
            picker.FileTypeFilter.Add("*");

            SettingsHost.OwnPicker(picker);

            var folder = await picker.PickSingleFolderAsync();

            if (folder is null)
            {
                return; // ignore: the user cancelled the dialog.
            }

            chosen(folder.Path);
        }
        catch (Exception ex)
        {
            // The dialog is a convenience over a line that still works. Losing it must not
            // take the window it was opened from down with it.
            Log.Error(Scope, "the folder picker could not be shown", ex);
        }
    }
}
