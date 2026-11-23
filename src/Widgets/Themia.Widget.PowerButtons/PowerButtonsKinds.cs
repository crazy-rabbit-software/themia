using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Widget.PowerButtons;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("shutdown", typeof(ShutdownModule))]
[assembly: ThemiaWidget("restart", typeof(RestartModule))]
[assembly: ThemiaWidget("sleep", typeof(SleepModule))]
[assembly: ThemiaWidget("hibernate", typeof(HibernateModule))]
[assembly: ThemiaWidget("lock", typeof(LockModule))]
[assembly: ThemiaWidget("signOut", typeof(SignOutModule))]
[assembly: ThemiaWidget("screenOff", typeof(ScreenOffModule))]

namespace Themia.Widget.PowerButtons;

/// <summary>
/// [S-1910] The seven power buttons as data, one action each: the family project's kinds, each with its own id, config and
/// words, the one view (<c>PowerButtonView</c>) and rules (<c>PowerButton</c>, <c>PowerActionRules</c>) the SDK's. The order
/// here is the add page's (Devices &amp; power › Power buttons).
/// </summary>
internal static class PowerButtonKinds
{
    /// <summary>Every kind, in the add page's order.</summary>
    public static IReadOnlyList<PowerButtonKind> All { get; } =
    [
        new("shutdown", () => new ShutdownConfig(), Glyphs.Power,
            PowerButtonsStr.Power_Action_Shutdown.Local(), PowerButtonsStr.WidgetsC_Power_Desc_Shutdown.Local(),
            ["power", "shutdown", "power off", "turn off", "switch off"], 0, 0),
        new("restart", () => new RestartConfig(), Glyphs.Reset,
            PowerButtonsStr.Power_Action_Restart.Local(), PowerButtonsStr.WidgetsC_Power_Desc_Restart.Local(),
            ["power", "reboot", "restart", "reset"], 0, 1),
        new("sleep", () => new SleepConfig(), Glyphs.Moon,
            PowerButtonsStr.Power_Action_Sleep.Local(), PowerButtonsStr.WidgetsC_Power_Desc_Sleep.Local(),
            ["power", "standby", "suspend"], 0, 2),
        new("hibernate", () => new HibernateConfig(), Glyphs.Hibernate,
            PowerButtonsStr.Power_Action_Hibernate.Local(), PowerButtonsStr.WidgetsC_Power_Desc_Hibernate.Local(),
            ["power", "suspend to disk", "deep sleep"], 0, 3),
        new("lock", () => new LockConfig(), Glyphs.Lock,
            PowerButtonsStr.Power_Action_Lock.Local(), PowerButtonsStr.WidgetsC_Power_Desc_Lock.Local(),
            ["power", "lock screen", "away", "security"], 0, 4),
        new("signOut", () => new SignOutConfig(), Glyphs.SignOut,
            PowerButtonsStr.Power_Action_SignOut.Local(), PowerButtonsStr.WidgetsC_Power_Desc_SignOut.Local(),
            ["power", "log off", "log out", "logout", "sign off"], 0, 5),
        new("screenOff", () => new ScreenOffConfig(), Glyphs.Display,
            PowerButtonsStr.Power_Action_ScreenOff.Local(), PowerButtonsStr.WidgetsC_Power_Desc_ScreenOff.Local(),
            ["power", "monitor off", "screen off", "blank screen", "display"], 0, 6),
    ];

    /// <summary>The kind of <paramref name="id"/>.</summary>
    public static PowerButtonKind Of(string id) => All.Single(k => k.Id == id);
}

/// <summary>One kind of the family as data: its id, its config (which action it is), its words, its add-page place.</summary>
internal sealed record PowerButtonKind(
    string Id,
    Func<PowerButtonConfig> Create,
    string Glyph,
    LocalText Label,
    LocalText Description,
    IReadOnlyList<string> Keywords,
    int Group,
    int Rank);

/// <summary>[S-6902] A kind's module: its descriptor from its row of <see cref="PowerButtonKinds"/>; the one view.</summary>
public abstract class PowerButtonModule : IWidgetModule
{
    /// <param name="id">The kind's id: its row of the table.</param>
    protected PowerButtonModule(string id)
    {
        var kind = PowerButtonKinds.Of(id);
        Descriptor = new()
        {
            Id = new(kind.Id),
            ConfigType = kind.Create().GetType(),
            CreateDefault = kind.Create,
            Json = PowerButtonsJsonContext.Default,
            Keywords = kind.Keywords,
            Label = kind.Label,
            Description = kind.Description,
            Glyph = kind.Glyph,
            // [S-17151] A power button's Switch row keeps the room for its why line (armed, failed); its tile none at rest.
            Sizing = c => TileRules.Sizing(((PowerButtonConfig)c).Look, stateLine: ((PowerButtonConfig)c).Look == TileLook.Switch),
            Traits = WidgetTraits.HasItems | WidgetTraits.OneReadout,
            Add = new(AddFolderId.PowerButtons, kind.Group, kind.Rank),
        };
    }

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; }

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(Descriptor.Id, host);
}

// [S-6901] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything else
// is the family's.

/// <summary>[S-7400] The Shutdown power button: one action, the whole tile its button.</summary>
public sealed class ShutdownModule() : PowerButtonModule("shutdown");

/// <summary>[S-7500] The Restart power button: one action, the whole tile its button.</summary>
public sealed class RestartModule() : PowerButtonModule("restart");

/// <summary>[S-7600] The Sleep power button: one action, the whole tile its button.</summary>
public sealed class SleepModule() : PowerButtonModule("sleep");

/// <summary>[S-7700] The Hibernate power button: one action, the whole tile its button.</summary>
public sealed class HibernateModule() : PowerButtonModule("hibernate");

/// <summary>[S-7800] The Lock power button: one action, the whole tile its button.</summary>
public sealed class LockModule() : PowerButtonModule("lock");

/// <summary>[S-7900] The SignOut power button: one action, the whole tile its button.</summary>
public sealed class SignOutModule() : PowerButtonModule("signOut");

/// <summary>[S-8000] The ScreenOff power button: one action, the whole tile its button.</summary>
public sealed class ScreenOffModule() : PowerButtonModule("screenOff");

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ShutdownConfig))]
[JsonSerializable(typeof(RestartConfig))]
[JsonSerializable(typeof(SleepConfig))]
[JsonSerializable(typeof(HibernateConfig))]
[JsonSerializable(typeof(LockConfig))]
[JsonSerializable(typeof(SignOutConfig))]
[JsonSerializable(typeof(ScreenOffConfig))]
internal sealed partial class PowerButtonsJsonContext : JsonSerializerContext;
