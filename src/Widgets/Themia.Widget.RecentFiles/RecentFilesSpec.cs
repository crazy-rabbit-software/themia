using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.RecentFiles;

/// <summary>
/// [S-4840] [S-11200] Recent files on the kit ([S-7074]): rows of the rich look, the file's icon as the lead, its name
/// the title, when it was opened the value (the kit's compact age), its folder on the second line where Lines says so.
/// A click focuses, a double-click or Enter opens the file ([S-7065]).
/// </summary>
public static class RecentFilesSpec
{
    /// <summary>The Open action's id: the view opens the file through the host's launcher.</summary>
    public const string OpenId = "open";

    /// <summary>"Open file location": the view shows the file in Explorer.</summary>
    public const string LocationId = "location";

    /// <summary>"Remove from list": the view takes it off Windows' Recent Items.</summary>
    public const string RemoveId = "remove";

    public static KitSpec Spec { get; } = new(
        KitRowLook.Rich,
        KitShow.Lead | KitShow.Subtitle | KitShow.When,
        static () => string.Empty,
        static () => RecentFilesStr.RecentFiles_Empty.Text(),
        KitTime.Compact);

    /// <summary>
    /// [S-4840] A file as a kit item: its path's fingerprint the id (a path may be longer than an id), its name the title,
    /// its folder the subtitle, when it was opened the time, its path in the lead (the view draws the shell's icon for
    /// it) and the tooltip with the day and time.
    /// </summary>
    public static KitItem Item(RecentFile file, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(file);

        var opened = file.Opened.LocalDateTime;
        return new KitItem(
            KitItems.Fingerprint(file.Path),
            file.Name,
            Subtitle: file.Folder,
            Lead: new KitLead(KitLeadKind.Icon, file.Path),
            When: KitWhen.Instant(file.Opened),
            Tooltip: $"{file.Path}{Environment.NewLine}{LocaleFormat.Date(locale, opened, DateStyle.Medium)} {LocaleFormat.Time(locale, opened)}");
    }

    /// <summary>[S-4841] A file's menu: Open (Enter, a double-click), Open file location, then Remove from list (Delete).</summary>
    public static IReadOnlyList<KitAction> Actions(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return
        [
            new KitAction(KitActionKind.Open, Str.Common_Open.Text(), KitIcon.Open, Id: OpenId),
            new KitAction(KitActionKind.OpenTo, Str.Shortcut_OpenFileLocation.Text(), KitIcon.Folder, Id: LocationId),
            new KitAction(KitActionKind.Delete, RecentFilesStr.RecentFiles_Remove.Text(), KitIcon.Clear, Id: RemoveId),
        ];
    }
}
