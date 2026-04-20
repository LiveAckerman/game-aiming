using System.Diagnostics;
using FPSToolbox.Models;
using FPSToolbox.Shared.Ipc;

namespace FPSToolbox.Core;

/// <summary>
/// 负责启动 / 停止子工具进程，并维护其运行时状态。
/// </summary>
public class ToolManager
{
    private readonly ToolRegistry _registry;
    private readonly IpcServer _ipcServer;
    private readonly string _pipeName;
    private readonly Dictionary<string, ToolRuntimeInfo> _runtime = new();
    private readonly object _lock = new();

    public event Action<string>? StateChanged;

    public ToolManager(ToolRegistry registry, IpcServer ipcServer, string pipeName)
    {
        _registry = registry;
        _ipcServer = ipcServer;
        _pipeName = pipeName;

        _ipcServer.OnSessionConnected = OnSessionConnected;
        _ipcServer.OnSessionDisconnected = OnSessionDisconnected;
    }

    public ToolRuntimeInfo Get(string name)
    {
        lock (_lock)
        {
            if (!_runtime.TryGetValue(name, out var info))
            {
                info = new ToolRuntimeInfo { Name = name };
                _runtime[name] = info;
            }
            info.State = ResolveState(info);
            return info;
        }
    }

    private ToolRuntimeState ResolveState(ToolRuntimeInfo info)
    {
        if (info.State is ToolRuntimeState.Starting or ToolRuntimeState.Running
            or ToolRuntimeState.Disconnected)
            return info.State;

        return _registry.IsInstalled(info.Name)
            ? ToolRuntimeState.Installed
            : ToolRuntimeState.NotInstalled;
    }

    public bool Start(string name)
    {
        var tool = _registry.Get(name);
        if (tool == null || !System.IO.File.Exists(tool.ExePath))
            return false;

        var info = Get(name);
        if (info.State is ToolRuntimeState.Starting or ToolRuntimeState.Running)
            return true;

        info.State = ToolRuntimeState.Starting;
        StateChanged?.Invoke(name);

        var psi = new ProcessStartInfo
        {
            FileName = tool.ExePath,
            Arguments = $"--parent-pid {Environment.ProcessId} --pipe {_pipeName}",
            UseShellExecute = false,
            WorkingDirectory = System.IO.Path.GetDirectoryName(tool.ExePath)!,
        };

        try
        {
            var proc = Process.Start(psi);
            if (proc == null) { info.State = ToolRuntimeState.Installed; return false; }
            info.Pid = proc.Id;
            proc.EnableRaisingEvents = true;
            proc.Exited += (_, _) =>
            {
                lock (_lock)
                {
                    info.State = ToolRuntimeState.Installed;
                    info.Pid = null;
                    info.Session = null;
                }
                StateChanged?.Invoke(name);
            };
            return true;
        }
        catch
        {
            info.State = ToolRuntimeState.Installed;
            StateChanged?.Invoke(name);
            return false;
        }
    }

    public async Task StopAsync(string name)
    {
        ToolRuntimeInfo? info;
        lock (_lock) { _runtime.TryGetValue(name, out info); }
        if (info?.Session == null) return;

        try
        {
            await info.Session.SendRequestAsync(IpcActions.Shutdown,
                timeout: TimeSpan.FromSeconds(2));
        }
        catch { }

        // 最多等 2 秒；还没退就强杀
        await Task.Delay(2000);
        if (info.Pid is int pid)
        {
            try { var p = Process.GetProcessById(pid); if (!p.HasExited) p.Kill(); } catch { }
        }
    }

    public async Task StopAllAsync()
    {
        List<string> names;
        lock (_lock) { names = _runtime.Keys.ToList(); }
        foreach (var name in names) await StopAsync(name);
    }

    private void OnSessionConnected(IpcSession session)
    {
        session.OnEvent = msg => HandleEvent(session, msg);
    }

    private void OnSessionDisconnected(IpcSession session)
    {
        if (string.IsNullOrEmpty(session.ToolName)) return;
        lock (_lock)
        {
            if (_runtime.TryGetValue(session.ToolName, out var info) && info.Session == session)
            {
                info.Session = null;
                info.State = _registry.IsInstalled(info.Name)
                    ? ToolRuntimeState.Installed
                    : ToolRuntimeState.NotInstalled;
            }
        }
        StateChanged?.Invoke(session.ToolName);
    }

    private void HandleEvent(IpcSession session, IpcMessage msg)
    {
        if (msg.Topic == IpcTopics.ToolReady)
        {
            var payload = msg.GetPayload<ToolReadyPayload>();
            if (payload?.Tool != null)
            {
                session.ToolName = payload.Tool;
                session.ToolPid = payload.Pid;
                lock (_lock)
                {
                    var info = Get(payload.Tool);
                    info.Session = session;
                    info.State = ToolRuntimeState.Running;
                    info.LastSeen = DateTime.Now;
                    info.Pid = payload.Pid;
                }
                StateChanged?.Invoke(payload.Tool);
            }
        }
        else if (msg.Topic == IpcTopics.ToolExiting && !string.IsNullOrEmpty(session.ToolName))
        {
            // 退出事件由 session disconnect 统一处理
        }

        // 其它 topic 由 MainWindow 订阅层处理（如准心可见性 → 更新托盘菜单勾选）
        OnToolEvent?.Invoke(session, msg);
    }

    public event Action<IpcSession, IpcMessage>? OnToolEvent;

    private sealed class ToolReadyPayload
    {
        public string? Tool { get; set; }
        public string? Version { get; set; }
        public int Pid { get; set; }
    }
}
