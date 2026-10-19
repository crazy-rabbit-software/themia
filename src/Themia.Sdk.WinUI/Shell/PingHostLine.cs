using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>[S-109] The host line the ping meter's and the ping details' properties menus share.</summary>
public static class PingHostLine
{
    /// <summary>
    /// [S-109] The host: the line shows the host measured (the default resolver while none is typed)
    /// and asks for another beside the menu.
    /// <para>
    /// This is the trust boundary: whatever is typed is persisted and later handed to a name
    /// resolver, so it is checked as it is typed (<see cref="Refuse"/>) and a value that is not a
    /// plausible host cannot be given at all: the widget keeps measuring the last host that was,
    /// rather than storing a typo that would read as a network fault. An empty box is not a
    /// rejection: it means the default resolver.
    /// </para>
    /// </summary>
    public static ItemMenuEntry Line(WidgetMenuLines menu, string host, Action<string> set)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(set);
        host ??= string.Empty;

        var title = Str.WidgetsB_Settings_Ping_Host_Label.Text();
        return new(Glyphs.Link, title, () => menu.Ask(new WidgetTextAsk(
            title,
            HostAddress.DefaultPingHost,
            Str.Common_Save.Text(),
            Refuse,
            typed =>
            {
                var value = (typed ?? string.Empty).Trim();
                if (Refuse(value) is null && value != host)
                {
                    set(value);
                    menu.Save();
                }
            },
            Initial: host,
            AllowEmpty: true)), Value: host.Length == 0 ? HostAddress.DefaultPingHost : host);
    }

    /// <summary>[S-109] Why a typed host cannot be measured, or null: empty (the default) or a plausible name or address.</summary>
    public static string? Refuse(string typed)
    {
        var value = (typed ?? string.Empty).Trim();
        return value.Length == 0 || HostAddress.IsPlausible(value) ? null : Str.WidgetsB_Settings_Ping_NotAHost.Text();
    }
}
