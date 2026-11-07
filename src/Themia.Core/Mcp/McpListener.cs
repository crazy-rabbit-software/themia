using System.Net;
using System.Net.Sockets;
using Themia.Core.Diagnostics;

namespace Themia.Core.Mcp;

/// <summary>
/// [S-9961] [S-9965] The socket under the server: a TCP listener on 127.0.0.1 only (never any other
/// address, never IPv6's any), one request per connection read under <see cref="McpHttp"/>'s limits,
/// at most <see cref="MaxConnections"/> at once. Disposing it stops listening at once and drops the
/// connections being served.
/// </summary>
public sealed class McpListener : IAsyncDisposable
{
    private const string Scope = "mcp";

    /// <summary>[S-9965] The most connections served at once; another is answered 503 and closed.</summary>
    public const int MaxConnections = 8;

    /// <summary>
    /// [S-9965] How long a client has to send its whole request. A resource bound (a stalled client
    /// cannot hold a slot), not a correctness wait.
    /// </summary>
    public static readonly TimeSpan ReadLimit = TimeSpan.FromSeconds(10);

    /// <summary>[S-9965] How long a tool may take before the call is answered with an error (the UI thread is busy).</summary>
    public static readonly TimeSpan AnswerLimit = TimeSpan.FromSeconds(30);

    private readonly TcpListener _listener;
    private readonly McpProtocol _protocol;
    private readonly string _token;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _slots = new(MaxConnections, MaxConnections);
    private readonly Task _accepting;

    private McpListener(TcpListener listener, McpProtocol protocol, string token)
    {
        _listener = listener;
        _protocol = protocol;
        _token = token;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _accepting = AcceptAsync();
    }

    /// <summary>The port it listens on: the one asked for, or the one Windows gave for 0.</summary>
    public int Port { get; }

    /// <summary>
    /// [S-9961] Listen on 127.0.0.1:<paramref name="port"/> (0 for any free port). Throws
    /// <see cref="SocketException"/> when the port is taken or refused; the address is never shared
    /// with another socket.
    /// </summary>
    public static McpListener Start(int port, string token, McpProtocol protocol)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);

        var listener = new TcpListener(IPAddress.Loopback, port) { ExclusiveAddressUse = true };
        listener.Start(backlog: MaxConnections);
        return new McpListener(listener, protocol, token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_stop.IsCancellationRequested)
        {
            return;
        }

        // Synchronous, before the first await: the socket is closed when this call returns to its caller.
        _stop.Cancel();
        _listener.Stop();
        try
        {
            await _accepting.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "the listener ended with an error", ex);
        }

        // _stop is not disposed: connections still winding down read its (cancelled) token.
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException ex)
            {
                if (_stop.IsCancellationRequested)
                {
                    return;
                }

                Log.Warn(Scope, "accepting a connection failed", ex);
                continue;
            }

            _ = ServeAsync(client);
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                // Bound to 127.0.0.1 a remote peer cannot connect; checked again all the same.
                if (client.Client.RemoteEndPoint is not IPEndPoint { Address: var peer } || !IPAddress.IsLoopback(peer))
                {
                    Log.Warn(Scope, "a connection that is not from this PC was dropped");
                    return;
                }

                var stream = client.GetStream();
                if (!await _slots.WaitAsync(0).ConfigureAwait(false))
                {
                    await WriteAsync(stream, McpHttp.Refusal(new McpHttpVerdict(503, "too many connections", 0)), CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                try
                {
                    var answer = await AnswerAsync(stream).ConfigureAwait(false);
                    await WriteAsync(stream, answer, _stop.Token).ConfigureAwait(false);
                }
                finally
                {
                    _slots.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // ignore: the server stopped, or the client was too slow; the connection is dropped.
            }
            catch (IOException ex)
            {
                Log.Debug(Scope, $"a connection broke: {ex.Message}");
            }
            catch (SocketException ex)
            {
                Log.Debug(Scope, $"a connection broke: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Nothing escapes a connection: the server keeps serving the next.
                Log.Error(Scope, "serving a request failed", ex);
            }
        }
    }

    /// <summary>Read one request and give the bytes of its answer.</summary>
    private async Task<byte[]> AnswerAsync(NetworkStream stream)
    {
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        reading.CancelAfter(ReadLimit);

        // The head: read until its blank line, never past the limit (and the body's first bytes kept).
        var buffer = new byte[McpHttp.MaxHeadBytes + 4];
        var filled = 0;
        int headEnd;
        while ((headEnd = McpHttp.HeadEnd(buffer.AsSpan(0, filled))) < 0)
        {
            if (filled == buffer.Length)
            {
                return McpHttp.Refusal(new McpHttpVerdict(431, "the request head is too large", 0));
            }

            var read = await stream.ReadAsync(buffer.AsMemory(filled), reading.Token).ConfigureAwait(false);
            if (read == 0)
            {
                throw new IOException("the client closed before its request was whole");
            }

            filled += read;
        }

        var head = McpHttp.ParseHead(buffer.AsSpan(0, headEnd), out var problem);
        if (head is null)
        {
            Log.Warn(Scope, $"a request was refused: {problem}");
            return McpHttp.Refusal(new McpHttpVerdict(400, problem, 0));
        }

        var verdict = McpHttp.Check(head, Port, _token);
        if (!verdict.Proceeds)
        {
            Log.Warn(Scope, $"a request was refused ({verdict.Status}): {verdict.Problem}");
            return McpHttp.Refusal(verdict);
        }

        // The body: what came with the head, then the rest of its declared length.
        var body = new byte[verdict.BodyLength];
        var already = Math.Min(filled - (headEnd + 4), body.Length);
        buffer.AsSpan(headEnd + 4, already).CopyTo(body);
        await stream.ReadExactlyAsync(body.AsMemory(already), reading.Token).ConfigureAwait(false);

        using var answering = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        answering.CancelAfter(AnswerLimit);
        var incoming = JsonRpc.Parse(body);
        var reply = await _protocol.HandleAsync(incoming, answering.Token).ConfigureAwait(false);
        if (reply is null)
        {
            return McpHttp.Response(202, [], null);
        }

        // A message that could not be read at all is the client's error; anything else is a JSON-RPC answer.
        var status = incoming is JsonRpcRejected { Id: null } ? 400 : 200;
        return McpHttp.Response(status, reply, "application/json");
    }

    private static async Task WriteAsync(NetworkStream stream, byte[] bytes, CancellationToken cancellation)
    {
        await stream.WriteAsync(bytes, cancellation).ConfigureAwait(false);
        await stream.FlushAsync(cancellation).ConfigureAwait(false);
    }
}
