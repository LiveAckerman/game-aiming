using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;

namespace FPSToolbox.Shared.Ipc;

/// <summary>
/// 子工具使用的 IPC 客户端。长连接一个 Named Pipe 到 Toolbox。
/// 使用方式：
///   var client = new IpcClient(pipeName);
///   client.OnRequest = async req => await client.SendResponseAsync(req.Id, true);
///   await client.StartAsync(cancellationToken);
///   await client.SendEventAsync(IpcTopics.ToolReady, new { tool = "CrosshairTool" });
/// </summary>
public sealed class IpcClient : IAsyncDisposable
{
    private readonly string _pipeName;
    private NamedPipeClientStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private long _idCounter = 1_000_000;
    private readonly CancellationTokenSource _cts = new();
    private Task? _readLoop;

    public IpcClient(string pipeName)
    {
        _pipeName = pipeName;
    }

    /// <summary>收到来自服务端的 request 时触发。实现者应回复一个 Response。</summary>
    public Func<IpcMessage, Task>? OnRequest { get; set; }

    /// <summary>收到服务端的 event 时触发。</summary>
    public Action<IpcMessage>? OnEvent { get; set; }

    /// <summary>连接断开时触发。</summary>
    public Action? OnDisconnected { get; set; }

    public bool IsConnected => _stream?.IsConnected == true;

    public async Task StartAsync(CancellationToken ct = default)
    {
        _stream = new NamedPipeClientStream(".", _pipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        await _stream.ConnectAsync(5000, ct);

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
                if (line == null) break; // EOF
                var msg = IpcMessage.TryParse(line);
                if (msg == null) continue;

                switch (msg.Kind)
                {
                    case "request":
                        if (OnRequest != null) await OnRequest(msg);
                        else await SendResponseAsync(msg.Id, false, error: "no handler");
                        break;
                    case "event":
                        OnEvent?.Invoke(msg);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { /* pipe broken / decode error */ }
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
        // 客户端主动向服务端发请求目前不是主要使用场景；如需可扩展为带 TaskCompletionSource 的路由。
        var id = Interlocked.Increment(ref _idCounter);
        await SendAsync(IpcMessage.Request(id, action, payload));
        await Task.CompletedTask;
        return null;
    }

    private async Task SendAsync(IpcMessage msg)
    {
        if (_writer == null) return;
        await _writeLock.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(msg.Serialize());
        }
        catch { /* pipe broken */ }
        finally { _writeLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { if (_readLoop != null) await _readLoop.ConfigureAwait(false); } catch { }
        _reader?.Dispose();
        _writer?.Dispose();
        _stream?.Dispose();
        _cts.Dispose();
        _writeLock.Dispose();
    }
}
