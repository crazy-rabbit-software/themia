namespace Themia.Core.Widgets;

/// <summary>What the background menu depends on.</summary>
/// <param name="HasPath">The widget shows a folder at all.</param>
/// <param name="Listed">That folder was read (it can take new items and a paste).</param>
/// <param name="HasItems">Something is shown (Select all).</param>
/// <param name="ClipboardHasFiles">Files are on the clipboard (Paste).</param>
/// <param name="CanGoUp">Navigate mode with a parent to go to.</param>
public readonly record struct FolderBackgroundFacts(bool HasPath, bool Listed, bool HasItems, bool ClipboardHasFiles, bool CanGoUp);

/// <summary>
/// [S-6416]..[S-6418] Which commands the folder widget's menus offer for a selection (files, folders,
/// both) and for the background, in Windows 11 File Explorer's order and groups. The item menu is the
/// SDK's one rule (<see cref="FileMenu.Item"/>, the desktop icons' too); the background menu is the
/// widget's own. Pure; <c>FileMenuEntries</c> words and draws each command.
/// </summary>
public static class FolderMenu
{
    /// <summary>[S-6416] The item menu for <paramref name="selected"/>: <see cref="FileMenu.Item"/> in the folder widget.</summary>
    public static IReadOnlyList<FileMenuLine> Item(IReadOnlyList<FolderEntry> selected, bool navigates, bool clipboardHasFiles)
    {
        ArgumentNullException.ThrowIfNull(selected);
        return FileMenu.Item(
            [.. selected.Select(e => new FileMenuItem(e.Name, e.IsDirectory, CloudBadges.State(e.Attributes) != CloudState.None))],
            new FileMenuFacts(FileMenuPlace.Folder, navigates, clipboardHasFiles));
    }

    /// <summary>
    /// [S-6418] The background menu: Paste, Select all; Up (navigate mode), Refresh; New; Open in
    /// Explorer, Copy as path, Properties; Show more options. No View or Sort: how the widget looks is
    /// its settings' ([S-1746]).
    /// </summary>
    public static IReadOnlyList<FileMenuLine> Background(FolderBackgroundFacts facts)
    {
        List<FileMenuLine> lines =
        [
            new(FileCommand.Paste, facts.Listed && facts.ClipboardHasFiles),
            new(FileCommand.SelectAll, facts.HasItems),
            FileMenuLine.Separator,
        ];

        if (facts.CanGoUp)
        {
            lines.Add(new(FileCommand.Up));
        }

        lines.Add(new(FileCommand.Refresh));
        lines.Add(FileMenuLine.Separator);
        lines.Add(new(FileCommand.New, facts.Listed));
        lines.Add(FileMenuLine.Separator);
        lines.Add(new(FileCommand.OpenInExplorer, facts.HasPath));
        lines.Add(new(FileCommand.CopyAsPath, facts.HasPath));
        lines.Add(new(FileCommand.Properties, facts.HasPath));
        lines.Add(FileMenuLine.Separator);
        lines.Add(new(FileCommand.ShowMoreOptions, facts.HasPath));
        return lines;
    }
}
