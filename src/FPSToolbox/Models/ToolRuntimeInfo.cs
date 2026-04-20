using FPSToolbox.Shared.Ipc;

namespace FPSToolbox.Models;

/// <summary>
/// 工具的运行时状态（UI 层订阅）。
/// </summary>
public enum ToolRuntimeState
{
    NotInstalled,
    Installed,      // 已安装未运行
    Starting,
    Running,
    Disconnected    // 心跳失联
}

public class ToolRuntimeInfo
{
    public string Name { get; set; } = "";
    public ToolRuntimeState State { get; set; } = ToolRuntimeState.NotInstalled;
    public int? Pid { get; set; }
    public IpcSession? Session { get; set; }
    public DateTime? LastSeen { get; set; }
}
