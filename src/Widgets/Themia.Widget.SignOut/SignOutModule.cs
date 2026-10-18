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
using Themia.Widget.SignOut;

[assembly: ThemiaWidget("signOut", typeof(SignOutModule))]

namespace Themia.Widget.SignOut;

/// <summary>[S-7900] The SignOut power button: one action, the whole tile its button. SPEC.md beside this file.</summary>
public sealed class SignOutModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("signOut"),
        ConfigType = typeof(SignOutConfig),
        CreateDefault = () => new SignOutConfig(),
        Json = SignOutJsonContext.Default,
        Keywords = ["power", "log off", "log out", "logout", "sign off"],
        Label = SignOutStr.Power_Action_SignOut.Local(),
        Description = SignOutStr.WidgetsC_Power_Desc_SignOut.Local(),
        Glyph = Glyphs.SignOut,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 5),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SignOutConfig))]
internal sealed partial class SignOutJsonContext : JsonSerializerContext;
