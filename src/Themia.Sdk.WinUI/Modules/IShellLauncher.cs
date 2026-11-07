using Themia.App.Services;
using Themia.Core.Office;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// The one place anything is opened, as a widget asks for it (<see cref="IWidgetHost.Launcher"/>).
/// The host's launcher classifies every target on the literal string it is about to hand the shell
/// and never builds a command line; each kind of link has its own allow list. None of these throws:
/// they are called from click handlers on a surface that must stay up.
/// </summary>
public interface IShellLauncher
{
    /// <summary>[S-547] / [S-1268] Whether Outlook for Windows' <c>ms-outlook:</c> protocol is registered (read once per run).</summary>
    bool OutlookInstalled { get; }

    /// <summary>Open a target (an http(s) URL, a local or UNC path, a <c>shell:</c> name), or say why not.</summary>
    LaunchOutcome Open(string? target);

    /// <summary>Open the folder a file lives in.</summary>
    LaunchOutcome OpenContainingFolder(string? target);

    /// <summary>[S-547] Open a mail link: a provider's web link, an <c>ms-outlook://emails/…</c> deep link or a <c>mailto:</c>.</summary>
    LaunchOutcome OpenMailLink(string? link);

    /// <summary>[S-1268] Open a calendar event: a provider's https page or an <c>ms-outlook://events/…</c> deep link.</summary>
    LaunchOutcome OpenCalendarLink(string? link);

    /// <summary>[S-4822] Join an online meeting: an http(s), <c>msteams:</c> or <c>zoommtg:</c> link.</summary>
    LaunchOutcome OpenMeetingLink(string? link);

    /// <summary>[S-587] Write to, call or message a contact from its own address or number.</summary>
    LaunchOutcome OpenContactLink(ContactLinkKind kind, string? value);

    /// <summary>Open a page of Windows Settings (<c>ms-settings:</c>).</summary>
    LaunchOutcome OpenWindowsSettings(Uri page);

    /// <summary>[S-4406] Start an app by its AppUserModelID.</summary>
    LaunchOutcome OpenApp(string? appUserModelId);

    /// <summary>
    /// [S-2000] Open what a click on a widget's card opens (<see cref="Themia.App.Widgets.IWidgetOpens"/>): a
    /// <see cref="CardOpenKind.Target"/> as <see cref="Open"/> does, a Settings page as
    /// <see cref="OpenWindowsSettings"/>, an app as <see cref="OpenApp"/> (a desktop app already
    /// running brought to the front instead, [S-2003]). The value's gate is applied again here.
    /// </summary>
    LaunchOutcome OpenCard(CardOpen open);

    /// <summary>[S-4661] Eject a drive with the shell's own verb on its root; Windows reports how it went.</summary>
    void Eject(string? root);
}
