using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Mcp;

namespace Themia.Core.Settings;

/// <summary>[S-9979] Where the assistants' server stands.</summary>
public enum McpServerState
{
    /// <summary>Switched off: no socket is open.</summary>
    Off,

    /// <summary>Switched on, the listener not up yet.</summary>
    Starting,

    /// <summary>Listening on <see cref="McpServerStatus.Address"/>.</summary>
    Listening,

    /// <summary>Switched on, but it could not listen (<see cref="McpServerStatus.Problem"/>).</summary>
    Failed,
}

/// <summary>[S-9979] Why the server could not start.</summary>
public enum McpProblem
{
    None,

    /// <summary>Another program listens on the port the clients were given.</summary>
    PortTaken,

    /// <summary>The key could not be sealed or saved for this user.</summary>
    KeyUnreadable,
}

/// <summary>[S-9979] The server's state and, once it has one, its address (its port and token).</summary>
public sealed record McpServerStatus(McpServerState State, McpAddress? Address = null, McpProblem Problem = McpProblem.None);

/// <summary>[S-9979] What ⚙ › AI assistants reads and does beyond the state's switch.</summary>
public interface IMcpMenuHost
{
    McpServerStatus Status { get; }

    /// <summary>
    /// [S-9978] Put a setup on the clipboard, kept out of Windows' clipboard history and cloud
    /// clipboard (it holds the token); whether it landed.
    /// </summary>
    bool Copy(string text);

    /// <summary>[S-9977] A new port and token: clients set up with the old ones are refused from now on.</summary>
    void NewAddress();
}

/// <summary>
/// [S-9979] ⚙ › AI assistants: the one switch of the assistants' server (MCP, off by default), what
/// it is doing, how a client connects (the setups, copied with the token in them, never shown with
/// it) and what assistants can read.
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>[S-9979] The AI assistants line and its submenu; null where the host runs no server (a render's detached state).</summary>
    public static SettingsSubmenu? Assistants(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.Mcp is not { } mcp)
        {
            return null;
        }

        var on = host.State.Preferences.McpServer;
        var status = mcp.Status;
        var lines = new List<SettingsLine>
        {
            new SettingsToggle(Keys.McpServer, Str.Mcp_Server.Text(), Glyphs.Code, on,
                value => host.Mutate(state => state.Preferences.McpServer = value, relayout: false)),
            StatusNote(on, status),
        };

        if (on && status.Address is not null)
        {
            lines.Add(new SettingsSeparator(Keys.Mcp + "/-connect"));
            lines.Add(new SettingsNote(Keys.Mcp + "/connect", Str.Mcp_Connect.Text()));
            lines.Add(new SettingsCommand(Keys.McpCopyClaudeCode, Str.Mcp_CopyClaudeCode.Text(), Glyphs.Copy,
                () => CopySetup(host, mcp, Keys.McpCopyClaudeCode, McpAccess.ClaudeCodeCommand)));
            lines.Add(OutcomeNote(host.Outcomes, Keys.McpCopyClaudeCode));
            lines.Add(new SettingsCommand(Keys.McpCopyJson, Str.Mcp_CopyJson.Text(), Glyphs.Code,
                () => CopySetup(host, mcp, Keys.McpCopyJson, McpAccess.JsonConfig)));
            lines.Add(OutcomeNote(host.Outcomes, Keys.McpCopyJson));
        }

        if (on)
        {
            lines.Add(new SettingsCommand(Keys.McpNewAddress, Str.Mcp_NewAddress.Text(), Glyphs.Refresh, () => host.Confirm(new SettingsConfirm(
                Str.Mcp_NewAddressConfirm.Text(),
                Str.Mcp_NewAddressAccept.Text(),
                mcp.NewAddress,
                Str.Mcp_NewAddressDetail.Text()))));
        }

        lines.Add(new SettingsSeparator(Keys.Mcp + "/-what"));
        lines.Add(new SettingsNote(Keys.Mcp + "/what", Str.Mcp_What.Text()));

        return new SettingsSubmenu(Keys.Mcp, Str.Mcp_Menu.Text(), Glyphs.Code, lines)
        {
            Value = on ? Str.Toggle_On.Text() : Str.Toggle_Off.Text(),
        };
    }

    /// <summary>[S-9979] What the server is doing, under its switch: the address shown without its token.</summary>
    private static SettingsNote StatusNote(bool on, McpServerStatus status)
    {
        var key = Keys.McpServer + "/status";
        if (!on)
        {
            return new SettingsNote(key, Str.Mcp_Off.Text());
        }

        return status switch
        {
            { State: McpServerState.Listening, Address: { } address } => new SettingsNote(key, Str.Mcp_Listening.Format(("url", address.Url))),
            { State: McpServerState.Failed } => new SettingsNote(key, Str.Mcp_Failed.Format(("reason", status.Problem switch
            {
                McpProblem.PortTaken => Str.Mcp_PortTaken.Text(),
                _ => Str.Mcp_KeyUnreadable.Text(),
            })))
            {
                Critical = true,
            },
            _ => new SettingsNote(key, Str.Mcp_Starting.Text()) { Busy = true },
        };
    }

    /// <summary>[S-9978] Copy a setup made from the address as it is at the click, and say under the line how it went.</summary>
    private static void CopySetup(ISettingsMenuHost host, IMcpMenuHost mcp, string row, Func<McpAddress, string> setup)
    {
        if (mcp.Status.Address is not { } address)
        {
            return;
        }

        var copied = mcp.Copy(setup(address));
        host.Outcomes.Say(row, new MenuOutcome(copied ? Str.Mcp_Copied.Text() : Str.Mcp_CopyFailed.Text(), Failed: !copied));
    }
}
