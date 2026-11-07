using System.Net.Sockets;
using System.Security.Cryptography;
using Themia.App.Services.Mcp;
using Themia.Core.Diagnostics;
using Themia.Core.Mcp;
using Themia.Core.Settings;

namespace Themia.App.Services;

/// <summary>
/// [S-9960] [S-9961] The assistants' server (MCP): listening only while ⚙ › AI assistants has it on
/// (the state's <c>Preferences.McpServer</c>), on 127.0.0.1 with this install's token. Every state
/// change reconciles it, so the switch, an undo or a reset turn it on or off at once; nothing of it
/// runs for a detached state (a render) or before the app has started.
/// </summary>
internal sealed partial class AppHost : IMcpMenuHost
{
    private const string McpScope = "mcp";

    private McpListener? _mcp;
    private McpKeyFile? _mcpKey;
    private McpServerStatus _mcpStatus = new(McpServerState.Off);
    private bool _mcpStarted;

    /// <summary>[S-9979] What ⚙ › AI assistants reads; null for a detached state, which runs no server.</summary>
    public IMcpMenuHost? McpMenu => _store.Persists ? this : null;

    McpServerStatus IMcpMenuHost.Status => _mcpStatus;

    /// <summary>[S-9960] Follow the switch from now on; called once the app has started.</summary>
    private void StartMcp()
    {
        if (!_store.Persists)
        {
            return;
        }

        _mcpKey = new McpKeyFile(McpKeyFile.PathBeside(_store.Path));
        _mcpStarted = true;
        StateChanged += ReconcileMcp;
        ReconcileMcp();
    }

    private void StopMcp()
    {
        StateChanged -= ReconcileMcp;
        _mcpStarted = false;
        CloseMcp();
    }

    /// <summary>[S-9960] On while the switch is; off the moment it is not.</summary>
    private void ReconcileMcp()
    {
        var wanted = _mcpStarted && !_disposed && State.Preferences.McpServer;
        if (wanted == (_mcpStatus.State != McpServerState.Off))
        {
            return;
        }

        if (wanted)
        {
            OpenMcp(fresh: false);
        }
        else
        {
            CloseMcp();
        }
    }

    /// <summary>
    /// [S-9961] [S-9977] Listen at the stored address (or a new token on a free port the first time,
    /// which is then stored). A taken port, or a key that cannot be stored, is said in ⚙, never retried on a timer.
    /// </summary>
    private void OpenMcp(bool fresh)
    {
        CloseMcp();
        if (_mcpKey is null)
        {
            return;
        }

        _mcpStatus = new McpServerStatus(McpServerState.Starting);
        SettingsOutdated?.Invoke();

        var address = fresh ? new McpAddress(0, McpAccess.NewToken()) : _mcpKey.Load();
        try
        {
            var protocol = new McpProtocol(new McpTools(new HostMcpData(_dispatcher, this), () => DateTimeOffset.Now), AppInfo.Version);
            var listener = McpListener.Start(address.Port, address.Token, protocol);
            var listening = address with { Port = listener.Port };
            try
            {
                _mcpKey.Save(listening);
            }
            catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
            {
                // A token that cannot be kept would change at every start: refuse rather than serve one.
                Log.Error(McpScope, "the server's key could not be stored; it does not start", ex);
                _ = listener.DisposeAsync().AsTask();
                _mcpStatus = new McpServerStatus(McpServerState.Failed, null, McpProblem.KeyUnreadable);
                SettingsOutdated?.Invoke();
                return;
            }

            _mcp = listener;
            _mcpStatus = new McpServerStatus(McpServerState.Listening, listening);
            Log.Info(McpScope, $"listening on {listening}");
        }
        catch (SocketException ex)
        {
            Log.Warn(McpScope, $"could not listen on 127.0.0.1:{address.Port}", ex);
            _mcpStatus = new McpServerStatus(McpServerState.Failed, address.Port == 0 ? null : address, McpProblem.PortTaken);
        }

        SettingsOutdated?.Invoke();
    }

    /// <summary>[S-9960] Stop listening now: the socket closes before this returns, the connections being served are dropped.</summary>
    private void CloseMcp()
    {
        if (_mcp is { } listener)
        {
            _mcp = null;
            Log.Info(McpScope, "stopped");
            var closing = listener.DisposeAsync().AsTask();
            closing.ContinueWith(t => Log.Warn(McpScope, "stopping the server failed", t.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }

        if (_mcpStatus.State != McpServerState.Off)
        {
            _mcpStatus = new McpServerStatus(McpServerState.Off);
            SettingsOutdated?.Invoke();
        }
    }

    bool IMcpMenuHost.Copy(string text) => ClipboardWatcher.SetPrivateText(text);

    /// <summary>[S-9977] A new token on a new free port, stored: what was copied before is refused from now on.</summary>
    void IMcpMenuHost.NewAddress()
    {
        if (State.Preferences.McpServer && _mcpStarted)
        {
            OpenMcp(fresh: true);
        }
    }
}
