using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Email;

[assembly: ThemiaWidget("email", typeof(EmailModule))]

namespace Themia.Widget.Email;

/// <summary>Email: recent mail of the signed-in accounts, as a list, compact or a table. SPEC.md beside this file.</summary>
public sealed class EmailModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("email"),
        ConfigType = typeof(EmailConfig),
        CreateDefault = () => new EmailConfig(),
        Json = EmailJsonContext.Default,
        Keywords = ["email", "inbox", "outlook", "gmail", "imap", "messages", "unread"],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.Imap],
        Label = EmailStr.WidgetsA_Catalog_Mail_Label.Local(),
        Description = EmailStr.WidgetsA_Catalog_Mail_Description.Local(),
        Glyph = Glyphs.Mail,
        DefaultWidth = 380,
        DefaultHeight = 260,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 1, 0, Popular: 4),
    };

    /// <summary>[S-3719] [S-7512] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => EmailMenu.Lines(menu, (EmailConfig)config, SettingsHost.AccountsWith(HostFeatures.Mail));

    public FrameworkElement CreateView(IWidgetHost host) => new EmailView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(EmailConfig))]
internal sealed partial class EmailJsonContext : JsonSerializerContext;
