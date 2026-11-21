using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.RemovableDrives;

/// <summary>
/// [S-4662] [S-11027] The removable drives on the kit ([S-7074]): rows of the rich look held to two lines, the drive's
/// picture a 32 lead, its name as This PC names it, the used share the value (a reading, [S-7072]), the space line
/// under the name, the used bar in the meter's place (<see cref="RemovableDrivesPlan"/>). Eject is the row's menu and
/// the keys (Delete, E), never a button in the row.
/// </summary>
public static class RemovableDrivesSpec
{
    /// <summary>The Open action's id: the view opens the drive in Explorer.</summary>
    public const string OpenId = "open";

    /// <summary>The eject's id: the shell's own eject, which says how it went in Windows' own notice.</summary>
    public const string EjectId = "eject";

    public static KitSpec Spec { get; } = new(
        KitRowLook.Rich,
        KitShow.Lead | KitShow.Subtitle | KitShow.When,
        static () => string.Empty,
        static () => RemovableDrivesStr.Drives_Nothing.Text(),
        KitTime.None)
    {
        Readings = true,
    };

    /// <summary>
    /// [S-4661] A device as a kit item: its root the id, its title the name, the used share the value, the space line the
    /// subtitle, what it is the preview, its root in the lead (the view draws its picture), the name and space line the
    /// tooltip.
    /// </summary>
    public static KitItem Item(DriveEntry device, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(device);

        var name = DriveRules.Title(locale, device);
        var space = DriveRules.SpaceLine(locale, device);
        return new KitItem(
            device.Root,
            name,
            Subtitle: space,
            Preview: DriveRules.Description(locale, device),
            Lead: new KitLead(KitLeadKind.Icon, device.Root),
            Tooltip: $"{name} · {space}",
            Value: device.UsedFraction is { } used ? Formatting.Percent(locale, used) : null);
    }

    /// <summary>[S-4663] A device's menu: Open (a double-click, Enter), then Eject (Delete, E).</summary>
    public static IReadOnlyList<KitAction> Actions(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return
        [
            new KitAction(KitActionKind.Open, Str.Common_Open.Text(), KitIcon.Open, Id: OpenId),
            new KitAction(KitActionKind.Change, Str.Drives_Eject.Text(), KitIcon.Eject, Id: EjectId),
        ];
    }
}
