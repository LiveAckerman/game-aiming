using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace FPSToolbox.Shared.Ipc;

/// <summary>
/// Toolbox 主程序使用的 IPC 服务端。
/// 为每个连接上来的子工具启动一个会话，通过 <see cref="IpcSession"/> 发送指令 / 接收事件。
/// </summary>
public sealed class IpcServer : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;
    private readonly ConcurrentDictionary<int, IpcSession> _sessions = new();
    private int _sessionIdCounter;

    /// <summary>新会话建立（子工具连上来）时触发。</summary>
    public Action<IpcSession>? OnSessionConnected { get; set; }

    /// <summary>会话断开时触发。</summary>
    public Action<IpcSession>? OnSessionDisconnected { get; set; }

    public IpcServer(string pipeName) { _pipeName = pipeName; }

    public IReadOnlyCollection<IpcSession> Sessions => _sessions.Values.ToList();

    public void Start()
    {
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            }
            catch { await Task.Delay(200); continue; }

            try
            {
                await server.WaitForConnectionAsync(_cts.Token);
            }
            catch
            {
                server.Dispose();
                break;
            }

            var sid = Interlocked.Increment(ref _sessionIdCounter);
            var session = new IpcSession(sid, server);
            _sessions[sid] = session;
            OnSessionConnected?.Invoke(session);

            session.OnDisconnected = () =>
            {
                _sessions.TryRemove(sid, out _);
                OnSessionDisconnected?.Invoke(session);
            };
            session.Start();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        foreach (var s in _sessions.Values)
            await s.DisposeAsync();
        _sessions.Clear();
        try { if (_acceptLoop != null) await _acceptLoop; } catch { }
        _cts.Dispose();
    }
}

/// <summary>
/// 单个子工具的会话。由 <see cref="IpcServer"/> 创建。
/// 支持双向通信：服务端可以 SendRequestAsync 并 await 响应。
/// </summary>
public sealed class IpcSession : IAsyncDisposable
{
    public int SessionId { get; }
    public string? ToolName { get; set; }
    public int ToolPid { get; set; }

    private readonly NamedPipeServerStream _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _readLoop;
    private long _idCounter;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<IpcMessage>> _pending = new();

    public Action? OnDisconnected { get; set; }
    public Action<IpcMessage>? OnEvent { get; set; }
    public Func<IpcMessage, Task>? OnRequest { get; set; }

    internal IpcSession(int id, NamedPipeServerStream stream)
    {
        SessionId = id;
        _stream = stream;
    }

    internal void Start()
    {
        _reader = new StreamReader(_stream, new UTF8Encoding(false), false, 4096, leaveOpen: true);
        _writer = new StreamWriter(_stream, new UTF8Encoding(false), 4096, leaveOpen: true)
        { AutoFlush = true, NewLine = "\n" };
        _readLoop = Task.Run(ReadLoopAsync);
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var line = await _reader!.ReadLineAsync(_cts.Token);
                if (line == null) break;
                var msg = IpcMessage.TryParse(line);
                if (msg == null) continue;

                switch (msg.Kind)
                {
                    case "event":
                        OnEvent?.Invoke(msg);
                        break;
                    case "request":
                        if (OnRequest != null) await OnRequest(msg);
                        else await SendResponseAsync(msg.Id, false, error: "no handler");
                        break;
                    case "response":
                        if (_pending.TryRemove(msg.Id, out var tcs))
                            tcs.TrySetResult(msg);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            OnDisconnected?.Invoke();
        }
    }

    public Task SendEventAsync(string topic, object? payload = null)
        => SendAsync(IpcMessage.Event(topic, payload));

    public Task SendResponseAsync(long requestId, bool ok, object? data = null, string? error = null)
        => SendAsync(IpcMessage.Response(requestId, ok, data, error));

    public async Task<IpcMessage?> SendRequestAsync(string action, object? payload = null,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _idCounter);
        var tcs = new TaskCompletionSource<IpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        await SendAsync(IpcMessage.Request(id, action, payload));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(5));

        using (cts.Token.Register(() =>
        {
            if (_pending.TryRemove(id, out var t)) t.TrySetCanceled();
        }))
        {
            try { return await tcs.Task; }
            catch (OperationCanceledException) { return null; }
        }
    }

    private async Task SendAsync(IpcMessage msg)
    {
        if (_writer == null) return;
        await _writeLock.WaitAsync();
        try { await _writer.WriteLineAsync(msg.Serialize()); }
        catch { }
        finally { _writeLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { if (_readLoop != null) await _readLoop; } catch { }
        _reader?.Dispose();
        _writer?.Dispose();
        _stream.Dispose();
        _cts.Dispose();
        _writeLock.Dispose();
    }
}
