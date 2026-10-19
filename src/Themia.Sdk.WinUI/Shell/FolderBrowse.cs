using Microsoft.UI.Xaml.Controls;
using Themia.Core.Diagnostics;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// The shell's own folder picker behind a settings page's path box (the folder widget's and the
/// gallery's), so nobody has to type a path they could point at.
/// <para>
/// The picker needs an owner window: the settings window the click came from
/// (<see cref="SettingsHost.OwnPicker"/>). It writes into the same box the user types into and goes
/// through the same commit, so there is one path to the setting rather than two.
/// </para>
/// </summary>
public static class FolderBrowse
{
    private const string Scope = "folder-settings";

    public static Task IntoAsync(TextBox box, Action commit)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(commit);

        return PickAsync(path =>
        {
            box.Text = path;
            commit();
        });
    }

    /// <summary>
    /// [S-3730] The picker on its own: <paramref name="chosen"/> gets the folder picked, and nothing
    /// happens on a cancel. The properties menu's folder line ([S-3730]) and the path boxes above go
    /// through it, so there is one picker.
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
            // The dialog is a convenience over a box or a line that still works. Losing it must not
            // take the window it was opened from down with it.
            Log.Error(Scope, "the folder picker could not be shown", ex);
        }
    }
}
