namespace Themia.App.Services.Files;

/// <summary>[S-1401] What a <c>.lnk</c> carries that its target alone would lose.</summary>
public static class ShortcutLinks
{
    /// <summary>
    /// Whether a link must be kept as the link itself rather than replaced by its target: it passes
    /// arguments (a browser profile, <c>RAMMap64.exe -Ew</c>), or it names an icon of its own (a
    /// custom picture the user chose in the link's properties). Either is lost the moment the
    /// target's path stands in for the link (issue 190).
    /// </summary>
    /// <param name="arguments">The link's arguments (<c>IShellLinkW::GetArguments</c>).</param>
    /// <param name="iconLocation">The link's icon file (<c>IShellLinkW::GetIconLocation</c>), empty when it uses the target's.</param>
    public static bool KeepsLinkItself(string? arguments, string? iconLocation)
        => !string.IsNullOrWhiteSpace(arguments) || !string.IsNullOrWhiteSpace(iconLocation);
}
