using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BeerMoney.Core.Collections;

namespace BeerMoney.Core.Network
{
    /// <summary>
    /// Embedded WebSocket server for broadcasting bar data to dashboard clients.
    /// Uses HttpListener with WebSocket support (.NET Framework 4.5+).
    /// Listens on http://127.0.0.1:8422/ws/
    /// </summary>
    public sealed class WebSocketServer : IDisposable
    {
        private const int ReceiveBufferSize = 1024;

        private readonly int _port;
        private readonly Action<string> _log;
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<Guid, ClientConnection> _clients;
        private readonly CircularBuffer<string> _barHistory;
        private readonly object _historyLock = new object();
        private volatile bool _isRunning;

        public int ClientCount => _clients.Count;
        public bool IsRunning => _isRunning;

        public WebSocketServer(int port = 8422, Action<string> log = null, int historySize = 28)
        {
            _port = port;
            _log = log ?? (_ => { });
            _clients = new ConcurrentDictionary<Guid, ClientConnection>();
            _barHistory = new CircularBuffer<string>(historySize);
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _cts = new CancellationTokenSource();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/ws/");
                _listener.Start();
                _isRunning = true;
                _log($"[WS] Server started on port {_port}");

                // Accept connections on background thread
                Task.Run(() => AcceptLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                _log($"[WS] Start failed: {ex.Message}");
                _isRunning = false;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cts?.Cancel();

            // Close all client connections
            foreach (var kvp in _clients)
            {
                try
                {
                    if (kvp.Value.Socket.State == WebSocketState.Open)
                        kvp.Value.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down",
                            CancellationToken.None).Wait(1000);
                }
                catch (Exception ex)
                {
                    _log($"[WS] Error closing client {kvp.Key}: {ex.Message}");
                }
                finally
                {
                    kvp.Value.Dispose();
                }
            }
            _clients.Clear();

            try { _listener?.Stop(); }
            catch (Exception ex) { _log($"[WS] Error stopping listener: {ex.Message}"); }
            try { _listener?.Close(); }
            catch (Exception ex) { _log($"[WS] Error closing listener: {ex.Message}"); }
            _listener = null;
            _cts?.Dispose();
            _cts = null;

            _log("[WS] Server stopped");
        }

        /// <summary>
        /// Broadcast a bar JSON message to all connected clients.
        /// Also stores in history buffer for new client sync.
        /// </summary>
        public void BroadcastBar(string json)
        {
            lock (_historyLock)
                _barHistory.Add(json);

            if (_clients.IsEmpty) return;

            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            foreach (var kvp in _clients)
            {
                var id = kvp.Key;
                var client = kvp.Value;

                if (client.Socket.State != WebSocketState.Open)
                {
                    RemoveClient(id);
                    continue;
                }

                // Fire-and-forget send with per-client lock to prevent concurrent SendAsync
                Task.Run(async () =>
                {
                    try
                    {
                        await client.SendLock.WaitAsync();
                        try
                        {
                            await client.Socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        finally
                        {
                            client.SendLock.Release();
                        }
                    }
                    catch
                    {
                        RemoveClient(id);
                    }
                });
            }
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();

                    if (!context.Request.IsWebSocketRequest)
                    {
                        // Respond to non-WebSocket requests with health check
                        context.Response.StatusCode = 200;
                        var healthBytes = Encoding.UTF8.GetBytes("{\"status\":\"ok\",\"clients\":" + _clients.Count + "}");
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = healthBytes.Length;
                        await context.Response.OutputStream.WriteAsync(healthBytes, 0, healthBytes.Length);
                        context.Response.Close();
                        continue;
                    }

                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var clientId = Guid.NewGuid();
                    var client = new ClientConnection(wsContext.WebSocket);
                    _clients.TryAdd(clientId, client);
                    _log($"[WS] Client connected ({_clients.Count} total)");

                    // Send buffered bars to new client
                    _ = Task.Run(() => SendHistoryAndListen(clientId, client, ct));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        _log($"[WS] Accept error: {ex.Message}");
                }
            }
        }

        private async Task SendHistoryAndListen(Guid clientId, ClientConnection client, CancellationToken ct)
        {
            try
            {
                // Snapshot history under lock, then send (holding send lock to serialize with broadcasts)
                string[] snapshot;
                lock (_historyLock)
                {
                    int count = _barHistory.Count;
                    snapshot = new string[count];
                    for (int i = 0; i < count; i++)
                        snapshot[i] = _barHistory[i];
                }
                _log($"[WS] Sending {snapshot.Length} history bars to client");
                await client.SendLock.WaitAsync(ct);
                try
                {
                    for (int i = 0; i < snapshot.Length; i++)
                    {
                        if (snapshot[i] == null) continue;
                        var bytes = Encoding.UTF8.GetBytes(snapshot[i]);
                        await client.Socket.SendAsync(new ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text, true, ct);
                    }
                }
                finally
                {
                    client.SendLock.Release();
                }
                _log($"[WS] History send complete ({snapshot.Length} bars)");

                // Keep connection alive, read until close
                var buffer = new byte[ReceiveBufferSize];
                while (client.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await client.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                }
            }
            catch (Exception ex)
            {
                _log($"[WS] Client error: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                RemoveClient(clientId);
            }
        }

        private void RemoveClient(Guid id)
        {
            if (_clients.TryRemove(id, out var client))
            {
                client.Dispose();
                _log($"[WS] Client disconnected ({_clients.Count} remaining)");
            }
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Per-client state: WebSocket + SemaphoreSlim to serialize sends.
        /// </summary>
        private sealed class ClientConnection : IDisposable
        {
            public WebSocket Socket { get; }
            public SemaphoreSlim SendLock { get; }

            public ClientConnection(WebSocket socket)
            {
                Socket = socket;
                SendLock = new SemaphoreSlim(1, 1);
            }

            public void Dispose()
            {
                try { Socket.Dispose(); } catch { }
                SendLock.Dispose();
            }
        }
    }
}
