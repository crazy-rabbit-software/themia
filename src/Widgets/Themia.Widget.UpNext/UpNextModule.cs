using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services.Accounts;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Widget.UpNext;

[assembly: ThemiaWidget("upNext", typeof(UpNextModule))]

namespace Themia.Widget.UpNext;

/// <summary>[S-11408] After this: the meetings after the next one, a row each. SPEC.md beside this file.</summary>
public sealed class UpNextModule : IWidgetModule
{
    /// <summary>[S-11408] As wide as the Next meeting block it stands under in the ready-made group.</summary>
    internal static KitWidths Widths { get; } = new(260, 320, 520);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("upNext"),
        ConfigType = typeof(UpNextConfig),
        CreateDefault = () => new UpNextConfig(),
        Json = UpNextJsonContext.Default,
        Keywords = ["up next", "after", "later", "meetings", "agenda", "schedule", "calendar", "today"],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CalDav],
        Label = UpNextStr.Meeting_After.Local(),
        Description = UpNextStr.UpNext_Desc.Local(),
        Glyph = Glyphs.Calendar,
        Sizing = c => KitSizing.Sizing(((UpNextConfig)c).RowsShown, addFooter: false, Widths),
        SizeSets = (c, size) => KitSizing.SizeSets(c, size, ((UpNextConfig)c).RowsShown, addFooter: false),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Links = LinkUse.Of([LinkTopicId.HoveredEvent, LinkTopicId.HoveredPerson], [LinkTopicId.HoveredDate, LinkTopicId.HoveredEvent, LinkTopicId.HoveredPerson]),
        Add = new(AddFolderId.CalendarMail, 0, 2),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new UpNextView(host);

    /// <summary>[S-11408] How many meetings, then the calendars every calendar widget chooses, without their colours.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var upNext = (UpNextConfig)config;
        menu.Lines.AddRange(Themia.Sdk.WinUI.Kit.KitMenuLines.Rows(menu, upNext));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(OfficeMenuLines.CalendarAccounts(menu, upNext, SettingsHost.Office, colours: false));
    }
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UpNextConfig))]
internal sealed partial class UpNextJsonContext : JsonSerializerContext;
