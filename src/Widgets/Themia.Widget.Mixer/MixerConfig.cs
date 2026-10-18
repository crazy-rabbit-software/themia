using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-440] Per-application volume for one output device — Windows' default unless the user chose one.
/// <para>
/// No refresh interval, unlike the original: the list follows Core Audio's own notifications, so
/// there is no poll for a setting to tune.
/// </para>
/// </summary>
public sealed class MixerConfig : WidgetConfig
{
    /// <summary>
    /// [S-1221] The output device's endpoint id, or null to follow Windows' default output (and
    /// move with it when a headset is plugged in).
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>[S-1221] The chosen device's name when it was chosen, so an unplugged one is still named.</summary>
    public string? DeviceName { get; set; }

    /// <summary>[S-442] One row per application rather than one per audio session.</summary>
    public bool GroupByApp { get; set; } = true;

    /// <summary>[S-445] Include the Windows system-sounds row.</summary>
    public bool ShowSystemSounds { get; set; }

    /// <summary>[S-445] Only the apps playing right now.</summary>
    public bool OnlyActive { get; set; }

    /// <summary>[S-446] Each app's own icon rather than a generic glyph.</summary>
    public bool ShowIcons { get; set; } = true;

    public override WidgetConfig Clone() => new MixerConfig
    {
        DeviceId = DeviceId,
        DeviceName = DeviceName,
        GroupByApp = GroupByApp,
        ShowSystemSounds = ShowSystemSounds,
        OnlyActive = OnlyActive,
        ShowIcons = ShowIcons,
    };

    /// <summary>A blank id is "follow the default", stored as null so there is one way to say it.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (DeviceId is not null && string.IsNullOrWhiteSpace(DeviceId))
        {
            DeviceId = null;
            DeviceName = null;
            return ["mixer device id was blank; following the default output"];
        }

        return [];
    }
}
