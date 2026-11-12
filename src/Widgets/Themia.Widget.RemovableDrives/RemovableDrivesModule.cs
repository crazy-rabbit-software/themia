using Themia.Sdk.WinUI.Kit;
using Themia.Sdk.Kit;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.RemovableDrives;

[assembly: ThemiaWidget("removableDrives", typeof(RemovableDrivesModule))]

namespace Themia.Widget.RemovableDrives;

/// <summary>[S-4660] A row per USB stick, card, disc or external disk plugged in, each with its Eject. SPEC.md beside this file.</summary>
public sealed class RemovableDrivesModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("removableDrives"),
        ConfigType = typeof(RemovableDrivesConfig),
        CreateDefault = () => new RemovableDrivesConfig(),
        Json = RemovableDrivesJsonContext.Default,
        Keywords = ["usb", "usb stick", "flash drive", "sd card", "memory card", "external disk", "eject", "safely remove"],
        Label = RemovableDrivesStr.Drives_Removable_Label.Local(),
        Description = RemovableDrivesStr.Drives_Removable_Desc.Local(),
        Glyph = Glyphs.Usb,
        Sizing = c => KitSizing.Sizing(Shown((RemovableDrivesConfig)c), addFooter: false),
        SizeSets = (c, s) => KitSizing.SizeSets(c, s, Shown((RemovableDrivesConfig)c), addFooter: false),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Devices, 2, 2),
    };

    /// <summary>[S-11027] Its rows as drawn: the config's count, two lines each.</summary>
    internal static KitRowsChoice Shown(RemovableDrivesConfig config) => new(KitSizing.ClampRows(config.Rows), RemovableDrivesPlan.Lines);

    /// <summary>[S-11027] Its one line: how many rows ([S-2440]); a row always has its two lines.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        menu.Lines.AddRange(KitMenuLines.Rows(menu, (RemovableDrivesConfig)config, offersLines: false));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new RemovableDrivesView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RemovableDrivesConfig))]
internal sealed partial class RemovableDrivesJsonContext : JsonSerializerContext;
