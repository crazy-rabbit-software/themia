using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Mixer;

/// <summary>
/// [S-440] [S-7074] The mixer on the kit: an app's row in the mixer's own look (its icon, its name, what it is doing on
/// the second line, the slider and the mute in the meter's place), its menu Mute or Unmute (Space) and Reset to 100 %.
/// </summary>
public static class MixerSpec
{
    /// <summary>Mute or unmute the app (Space, the menu).</summary>
    public const string MuteId = "mute";

    /// <summary>Put the app back to 100 %.</summary>
    public const string ResetId = "reset";

    public static KitSpec Spec { get; } = new(
        KitRowLook.Rich,
        KitShow.Lead | KitShow.Subtitle,
        static () => MixerStr.Mixer_UnknownApp.Text(),
        static () => MixerStr.Mixer_Empty.Text(),
        KitTime.None);

    /// <summary>[S-441] An app's row as a kit item: its key the id, its name (the system sounds', or "Unknown app"), what it is doing.</summary>
    public static KitItem Item(MixerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var name = row.IsSystem ? MixerStr.Mixer_SystemSounds.Text() : row.Name;
        return new KitItem(row.Key, name, Subtitle: MixerFill.Detail(row), Lead: new KitLead(KitLeadKind.Icon, row.Path ?? (row.IsSystem ? "system" : "app")));
    }

    /// <summary>[S-447] An app's menu: Mute or Unmute, then Reset to 100 %; none on the stand-in row of a mixer with no device.</summary>
    public static IReadOnlyList<KitAction> Actions(MixerRow? row, bool available)
    {
        if (row is null || !available)
        {
            return [];
        }

        return
        [
            new KitAction(KitActionKind.Toggle, row.Muted ? Str.Mixer_Unmute.Text() : Str.Mixer_Mute.Text(), row.Muted ? KitIcon.Unmute : KitIcon.Mute, Id: MuteId),
            new KitAction(KitActionKind.Change, Str.Mixer_Reset.Format(("percent", LocaleFormat.Percent(Strings.Current, 100))), KitIcon.Reset, Id: ResetId),
        ];
    }
}
